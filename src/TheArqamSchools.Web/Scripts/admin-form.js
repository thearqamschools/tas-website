/* The Arqam Schools admin: "Clear" buttons next to optional time fields.
   Browsers make an <input type="time"> hard to empty, so each optional time gets a button that clears it. */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("[data-clear]"), function (btn) {
    var input = document.getElementById(btn.getAttribute("data-clear"));
    if (!input) return;
    function sync() { btn.hidden = !input.value; }
    btn.setAttribute("aria-label", input.id === "EndTime" ? "Clear end time" : "Clear start time");
    btn.addEventListener("click", function () { input.value = ""; sync(); input.focus(); });
    input.addEventListener("input", sync);
    input.addEventListener("change", sync);
    sync();
  });
})();

/* Bold and italic buttons for the news text: they wrap the selected words in ** or *.
   The website shows only these two styles (Helpers/SimpleFormat.cs); everything else stays plain text. */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("[data-format-bar]"), function (bar) {
    var area = document.getElementById(bar.getAttribute("data-format-bar"));
    if (!area || area.disabled) return;
    bar.hidden = false;
    Array.prototype.forEach.call(bar.querySelectorAll("[data-format]"), function (btn) {
      btn.addEventListener("click", function () {
        var mark = btn.getAttribute("data-format");
        var a = area.selectionStart, b = area.selectionEnd, v = area.value;
        var sel = v.slice(a, b);
        // Keep spaces outside the marks ("**word** " not "**word **").
        var lead = sel.match(/^\s*/)[0], trail = sel.match(/\s*$/)[0];
        var core = sel.slice(lead.length, sel.length - trail.length);
        var text = core || (mark === "**" ? "bold text" : "italic text");
        var insert = lead + mark + text + mark + trail;
        area.focus();
        area.setRangeText(insert, a, b, "end");
        var start = a + lead.length + mark.length;
        area.setSelectionRange(start, start + text.length);
        area.dispatchEvent(new Event("input", { bubbles: true }));
      });
    });
  });
})();

/* Buttons that ask first (for example "Remove" in a list). */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("button[data-confirm]"), function (btn) {
    btn.addEventListener("click", function (e) {
      if (!window.confirm(btn.getAttribute("data-confirm"))) e.preventDefault();
    });
  });
})();

/* Content forms: refuse to send more photos at once than the server accepts (20 MB per save). */
(function () {
  "use strict";
  var LIMIT = 18 * 1024 * 1024;
  Array.prototype.forEach.call(document.querySelectorAll('form[enctype="multipart/form-data"]'), function (form) {
    if (form.hasAttribute("data-photo-upload")) return; // news photos are sent one by one
    form.addEventListener("submit", function (e) {
      var total = 0;
      Array.prototype.forEach.call(form.querySelectorAll('input[type="file"]'), function (f) {
        Array.prototype.forEach.call(f.files || [], function (x) { total += x.size; });
      });
      if (total > LIMIT) {
        e.preventDefault();
        window.alert("The photos chosen add up to " + Math.round(total / 1048576) + " MB. Please save one or two photos at a time (up to 18 MB together).");
      }
    });
  });
})();

/* Book payments and returns: the account list shows only accounts of the chosen method (cash or bank). */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("[data-account-filter]"), function (method) {
    var account = document.getElementById(method.getAttribute("data-account-filter") || "AccountId");
    if (!account) return;
    function sync() {
      Array.prototype.forEach.call(account.options, function (o) {
        var kind = o.getAttribute("data-kind");
        if (!kind) return;
        o.hidden = kind !== method.value;
        o.disabled = kind !== method.value;
        if (o.selected && o.disabled) account.value = "";
      });
    }
    method.addEventListener("change", sync);
    sync();
  });
})();

/* Book form: sale price = retail price less the discount %, worked out as the office types. */
(function () {
  "use strict";
  var box = document.querySelector("[data-sale-calc]");
  if (!box) return;
  var retail = document.getElementById("RetailPrice"), pct = document.getElementById("DiscountPercent"), sale = document.getElementById("SalePrice");
  if (!retail || !pct || !sale) return;
  function num(el) { var v = (el.value || "").replace(/[,%\s]/g, ""); return v === "" ? null : Number(v); }
  function sync() {
    var r = num(retail), d = num(pct);
    if (r === null || isNaN(r)) { sale.readOnly = false; return; }
    if (d === null || isNaN(d)) d = 0;
    sale.readOnly = true;
    if (r >= 0 && d >= 0 && d <= 100) sale.value = String(Math.round(r * (100 - d)) / 100);
  }
  retail.addEventListener("input", sync);
  pct.addEventListener("input", sync);
  sync();
})();

/* Order lists: the whole row opens the order (the order number stays a normal link for the keyboard). */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("tr[data-href]"), function (row) {
    row.addEventListener("click", function (e) {
      if (e.target.closest("a, button, input, select, label")) return;
      if (window.getSelection && String(window.getSelection())) return;   // selecting text does not navigate
      if (e.ctrlKey || e.metaKey) window.open(row.getAttribute("data-href"), "_blank");
      else window.location.href = row.getAttribute("data-href");
    });
  });
})();

/* Messages: copy the text of a ready-made message. */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("[data-copy]"), function (btn) {
    btn.addEventListener("click", function () {
      var box = document.getElementById(btn.getAttribute("data-copy"));
      if (!box) return;
      var text = box.textContent;
      var done = function () { var old = btn.textContent; btn.textContent = "Copied"; setTimeout(function () { btn.textContent = old; }, 1500); };
      if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).then(done, function () {});
      else { var r = document.createRange(); r.selectNodeContents(box); var s = window.getSelection(); s.removeAllRanges(); s.addRange(r); try { document.execCommand("copy"); done(); } catch (e) {} }
    });
  });
})();
