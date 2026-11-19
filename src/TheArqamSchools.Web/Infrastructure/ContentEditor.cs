using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using Newtonsoft.Json.Linq;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// The shared "save" step of the content forms (page sections and the homepage): checks the
    /// form's signature, writes the posted words, stores newly chosen photos and applies the list
    /// button that was pressed (add, remove, move). Posted names: v{i} values, a{i} photo
    /// descriptions, f{i} new photo files, r{i} "remove photo", op the pressed button.
    /// </summary>
    public static class ContentEditor
    {
        public class Result
        {
            public IList<string> Errors = new List<string>();
            public IList<JsonContentFields.Field> PostedFields;
            /// <summary>Photos saved during this request; removed again by the caller when the database save fails.</summary>
            public IList<string> NewFiles = new List<string>();
            /// <summary>Address fragment to return to (#item-2), after a list change.</summary>
            public string Anchor;
            public bool SignatureMismatch;
        }

        public static Result Process(JObject root, string sectionType, HttpRequestBase request)
        {
            var result = new Result();
            var fields = JsonContentFields.Flatten(root, sectionType);
            var form = request.Unvalidated.Form;
            if (form["signature"] != JsonContentFields.Signature(fields))
            {
                result.SignatureMismatch = true;
                result.Errors.Add("This content was changed by someone else while you were editing. Your changes were not saved; please make them again.");
                return result;
            }

            var errors = JsonContentFields.Apply(root, fields, i => form["v" + i], i => form["a" + i]);
            if (errors.Count > 0)
            {
                for (var i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Kind == JsonContentFields.FieldKind.Image) fields[i].Alt = form["a" + i];
                    else fields[i].Value = form["v" + i];
                }
                result.Errors = errors;
                result.PostedFields = fields;
                return result;
            }

            // New photos and "remove photo" ticks.
            for (var i = 0; i < fields.Count; i++)
            {
                if (fields[i].Kind != JsonContentFields.FieldKind.Image) continue;
                var file = request.Files["f" + i];
                if (file != null && file.ContentLength > 0)
                {
                    try
                    {
                        var img = ImageStore.Save(file.InputStream, file.ContentLength, file.FileName, "pages", true);
                        result.NewFiles.Add(img.StoragePath);
                        JsonContentFields.SetImage(root, fields[i], img, CleanAlt(form["a" + i]));
                    }
                    catch (ImageRejectedException ex)
                    {
                        result.Errors.Add(fields[i].Label + ": " + ex.Message);
                    }
                }
                else if ((form["r" + i] ?? "").Split(',').Contains("true"))
                {
                    JsonContentFields.SetImage(root, fields[i], null, null);
                }
            }
            if (result.Errors.Count > 0)
            {
                foreach (var p in result.NewFiles) ImageStore.Delete(p);
                result.NewFiles.Clear();
                // Show what was typed, so a refused photo does not lose the other changes.
                for (var i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Kind == JsonContentFields.FieldKind.Image) fields[i].Alt = form["a" + i];
                    else fields[i].Value = form["v" + i];
                }
                result.PostedFields = fields;
                return result;
            }

            // The list button that was pressed, for example "add:items", "remove:items:2", "up:items:2".
            var op = (form["op"] ?? "save").Split(':');
            // Only lists of items (objects) such as stages, people or slides; not lists of paragraphs.
            var list = op.Length >= 2 ? root[op[1]] as JArray : null;
            if (list != null && list.Count > 0 && list[0].Type == JTokenType.Object)
            {
                int index = -1;
                if (op.Length >= 3) int.TryParse(op[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
                switch (op[0])
                {
                    case "add":
                        if (JsonContentFields.AddItem(root, op[1])) result.Anchor = "#item-" + op[1] + "-" + (((JArray)root[op[1]]).Count - 1);
                        break;
                    case "remove":
                        if (JsonContentFields.RemoveItem(root, op[1], index)) result.Anchor = "#list-" + op[1];
                        break;
                    case "up":
                        if (JsonContentFields.MoveItem(root, op[1], index, -1)) result.Anchor = "#item-" + op[1] + "-" + (index - 1);
                        break;
                    case "down":
                        if (JsonContentFields.MoveItem(root, op[1], index, 1)) result.Anchor = "#item-" + op[1] + "-" + (index + 1);
                        break;
                }
            }
            return result;
        }

        private static string CleanAlt(string alt)
        {
            alt = (alt ?? "").Replace("\r\n", " ").Replace("\n", " ").Trim();
            return alt.Length > 250 ? alt.Substring(0, 250) : alt;
        }

        private static bool Contains(this string[] values, string value)
        {
            return Array.IndexOf(values, value) >= 0;
        }
    }
}
