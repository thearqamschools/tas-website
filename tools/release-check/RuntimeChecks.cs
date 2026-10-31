// Runs against an isolated copy only. This is ASP.NET hosting, not an IIS test.
using System;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Collections.Generic;

public class RuntimeChecks : MarshalByRefObject
{
    public sealed class Worker : SimpleWorkerRequest
    {
        public int Status = 200;
        public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string method;
        public Worker(string page, string query, StringWriter output, string verb) : base(page, query, output) { method = verb; }
        public override bool IsSecure() { return true; }
        public override string GetHttpVerbName() { return method; }
        public override void SendStatus(int code, string description) { Status = code; }
        public override void SendKnownResponseHeader(int index, string value) { Headers[GetKnownResponseHeaderName(index)] = value; }
        public override void SendUnknownResponseHeader(string name, string value) { Headers[name] = value; }
    }
    public void Check(string page, string query, string verb, int expected)
    {
        var output = new StringWriter();
        var worker = new Worker(page, query, output, verb);
        HttpRuntime.ProcessRequest(worker);
        string body = output.ToString();
        if (worker.Status != expected) throw new Exception(page + " expected " + expected + " got " + worker.Status + " " + body);
        if (worker.Headers.ContainsKey("Location")) throw new Exception("Unexpected error redirect");
        if (body.Contains("PRIVATE_TEST_MARKER") || body.Contains("SqlException") || body.Contains("Stack Trace"))
            throw new Exception("Error detail leaked");
        string cache;
        if (!worker.Headers.TryGetValue("Cache-Control", out cache) || !cache.Contains("no-store"))
            throw new Exception("Error response may be cached");
        if (expected == 503 && (!body.Contains("Temporarily unavailable") || !worker.Headers.ContainsKey("Retry-After")))
            throw new Exception("Outage response missing retry information");
        Console.WriteLine("PASS " + verb + " /" + page + "?" + query + " => " + worker.Status + ", safe body, no redirect");
    }
    public static int Main(string[] args)
    {
        try
        {
            var host = (RuntimeChecks)ApplicationHost.CreateApplicationHost(typeof(RuntimeChecks), "/", args[0]);
            foreach (int code in new[] {400, 403, 404, 429, 500}) host.Check("release-test.aspx", "code=" + code, "GET", code);
            host.Check("release-test.aspx", "code=500&unsafe=%3Cscript%3E", "GET", 400);
            host.Check("admissions/inquiry", "", "POST", 400);
            host.Check("", "", "GET", 503);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
