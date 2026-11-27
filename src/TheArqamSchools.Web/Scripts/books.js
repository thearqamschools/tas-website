/* The Arqam Schools: books order pages (owner's notes 56-61, 27 November 2026; 30 November: the order form keeps the
   classes one after the other with quantity boxes, as before).
   - Line totals and the bar at the bottom (how many books, total) update as the customer types.
     The server works out the real totals again.
   - Copy buttons for bank account numbers, and the name of the chosen receipt. */
(function () {
  "use strict";
  var form = document.querySelector("[data-books-form]");
  if (!form) return;
  var totalEl = form.querySelector("[data-books-total]");
  var countEl = form.querySelector("[data-books-count]");
  var inputs = form.querySelectorAll("input[data-price]");
  var fmt = function (n) { return "Rs. " + (Math.round(n * 100) / 100).toLocaleString("en-US", { maximumFractionDigits: 2 }); };
  var max = function (inp) { return parseInt(inp.getAttribute("max"), 10) || 99; };
  var val = function (inp) { var q = parseInt(inp.value, 10); return q > 0 ? q : 0; };

  function update() {
    var total = 0, count = 0;
    Array.prototype.forEach.call(inputs, function (inp) {
      var q = val(inp);
      total += q * parseFloat(inp.getAttribute("data-price"));
      count += q;
      var row = inp.closest(".books-row") || inp.closest("tr");
      if (row) {
        row.classList.toggle("is-chosen", q > 0);
        var out = row.querySelector("[data-line-total]");   // the office's order form shows line totals
        if (out) out.textContent = q > 0 ? fmt(q * parseFloat(inp.getAttribute("data-price"))) : "";
      }
    });
    if (totalEl) totalEl.textContent = fmt(total);
    if (countEl) countEl.textContent = count + (count === 1 ? " book" : " books");
  }

  if (totalEl) totalEl.setAttribute("aria-live", "polite");
  form.addEventListener("input", update);
  update();
})();

/* Pressing a button twice must not place two orders or send a payment twice (review, 27 November 2026): after the
   first press the buttons are switched off. This happens just after the form is sent, so the pressed button's value
   still goes with it. Without JavaScript the server's rate limit still applies. */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll(".books-form, .books-pay-form"), function (form) {
    form.addEventListener("submit", function (e) {
      if (e.defaultPrevented || (form.checkValidity && !form.checkValidity())) return;   // site.js shows the errors
      if (form.getAttribute("data-busy")) { e.preventDefault(); return; }
      form.setAttribute("data-busy", "1");
      setTimeout(function () {
        Array.prototype.forEach.call(form.querySelectorAll("button[type=submit]"), function (b) { b.disabled = true; });
      }, 0);
      // If the page is shown again from the browser's history, the buttons work again.
      window.addEventListener("pageshow", function () {
        form.removeAttribute("data-busy");
        Array.prototype.forEach.call(form.querySelectorAll("button[type=submit]"), function (b) { b.disabled = false; });
      }, { once: true });
    });
  });
})();

/* Copy a bank account number; show the chosen receipt's name. */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("[data-copy-text]"), function (b) {
    b.addEventListener("click", function (e) {
      e.preventDefault();
      var text = b.getAttribute("data-copy-text");
      var done = function () { var old = b.textContent; b.textContent = "Copied"; setTimeout(function () { b.textContent = old; }, 1500); };
      if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).then(done, function () {});
    });
  });
  Array.prototype.forEach.call(document.querySelectorAll("[data-receipt]"), function (box) {
    var out = box.querySelector("[data-receipt-name]");
    var inputs = box.querySelectorAll("[data-receipt-input]");
    Array.prototype.forEach.call(inputs, function (inp) {
      inp.addEventListener("change", function () {
        // Only one receipt is sent: choosing one clears the other.
        Array.prototype.forEach.call(inputs, function (o) { if (o !== inp) o.value = ""; });
        var f = inp.files && inp.files[0];
        if (out) out.textContent = f ? "✓ " + f.name : "";
        box.classList.toggle("has-file", !!f);
      });
    });
  });
})();

/* Cancel order: ask once before sending (the server checks again that the order can still be cancelled). */
(function () {
  "use strict";
  Array.prototype.forEach.call(document.querySelectorAll("form[data-confirm-submit]"), function (form) {
    form.addEventListener("submit", function (e) {
      if (!window.confirm(form.getAttribute("data-confirm-submit"))) e.preventDefault();
    });
  });
})();
