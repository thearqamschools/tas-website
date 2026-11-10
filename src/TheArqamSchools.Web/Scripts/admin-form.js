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
