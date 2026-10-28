/* The Arqam Schools: header dropdowns, mobile menu and form focus. No libraries. */
(function () {
  "use strict";

  // Desktop dropdowns: only menu items with sub-pages have a button. Click opens, Escape or a click outside closes.
  var buttons = document.querySelectorAll("[data-dropdown]");
  function closeAll(except) {
    buttons.forEach(function (b) {
      if (b === except) return;
      b.setAttribute("aria-expanded", "false");
      var p = document.getElementById(b.getAttribute("aria-controls"));
      if (p) p.hidden = true;
    });
  }
  buttons.forEach(function (b) {
    b.addEventListener("click", function () {
      var open = b.getAttribute("aria-expanded") === "true";
      closeAll(b);
      b.setAttribute("aria-expanded", open ? "false" : "true");
      var p = document.getElementById(b.getAttribute("aria-controls"));
      if (p) p.hidden = open;
    });
  });
  document.addEventListener("click", function (e) {
    if (!e.target.closest(".has-dropdown")) closeAll(null);
  });
  document.addEventListener("keydown", function (e) {
    if (e.key !== "Escape") return;
    var open = document.querySelector("[data-dropdown][aria-expanded='true']");
    closeAll(null);
    if (open) open.focus();
  });

  // Mobile menu (a <dialog>, so focus stays inside while it is open).
  var dialog = document.getElementById("mobile-menu");
  var opener = document.querySelector("[data-menu-open]");
  if (dialog && opener && typeof dialog.showModal === "function") {
    opener.addEventListener("click", function () { dialog.showModal(); opener.setAttribute("aria-expanded", "true"); });
    dialog.addEventListener("close", function () { opener.setAttribute("aria-expanded", "false"); opener.focus(); });
    dialog.querySelectorAll("[data-menu-close]").forEach(function (b) { b.addEventListener("click", function () { dialog.close(); }); });
    dialog.querySelectorAll("a[href]").forEach(function (a) { a.addEventListener("click", function () { dialog.close(); }); });
    window.addEventListener("resize", function () { if (window.innerWidth >= 1280 && dialog.open) dialog.close(); });
  } else if (opener) {
    opener.addEventListener("click", function () { window.location.href = "/campuses"; });
  }

  // After a form is sent or has errors, move focus to the message so screen readers announce it.
  var msg = document.querySelector("[data-focus]");
  if (msg) msg.focus({ preventScroll: true });

  // Required consent checkboxes: browsers check them before sending; the server checks everything again.
  document.querySelectorAll("form[novalidate]").forEach(function (f) {
    f.addEventListener("submit", function (e) {
      if (!f.checkValidity()) {
        e.preventDefault();
        var first = f.querySelector(":invalid");
        if (first) { first.focus(); if (first.reportValidity) first.reportValidity(); }
      }
    });
  });
})();
