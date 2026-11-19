/* The Arqam Schools: books order form. Line totals and the order total update as the customer types.
   The server works out the real totals again; this is only to help while typing. */
(function () {
  "use strict";
  var form = document.querySelector("[data-books-form]");
  if (!form) return;
  var totalEl = form.querySelector("[data-books-total]");
  var fmt = function (n) { return "Rs. " + (Math.round(n * 100) / 100).toLocaleString("en-US", { maximumFractionDigits: 2 }); };
  function update() {
    var total = 0;
    Array.prototype.forEach.call(form.querySelectorAll("input[data-price]"), function (inp) {
      var q = parseInt(inp.value, 10);
      if (!(q > 0)) q = 0;
      var line = q * parseFloat(inp.getAttribute("data-price"));
      total += line;
      var row = inp.closest ? (inp.closest("tr") || inp.closest(".books-row") || inp.parentNode) : inp.parentNode;
      var out = row.querySelector("[data-line-total]");
      if (out) out.textContent = q > 0 ? fmt(line) : "";
      row.classList.toggle("is-chosen", q > 0);
    });
    if (totalEl) totalEl.textContent = fmt(total);
  }
  form.addEventListener("input", update);
  update();
})();

/* Cancel order: ask once before sending (the server checks again that the order is still New). */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("form[data-confirm-submit]"), function (form) {
    form.addEventListener("submit", function (e) {
      if (!window.confirm(form.getAttribute("data-confirm-submit"))) e.preventDefault();
    });
  });
})();
