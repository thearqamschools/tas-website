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
