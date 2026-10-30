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

  // Homepage scroll reveal: a one-time fade and 14px rise for blocks below the first screen.
  // Content is fully visible without JS: CSS only hides blocks once js-reveal is on <html>, and that happens only here.
  // Skipped for reduced motion, Save-Data, a #hash in the URL, or browsers without IntersectionObserver.
  (function () {
    var items = document.querySelectorAll(".home-reveal");
    if (!items.length || !("IntersectionObserver" in window)) return;
    var reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var saveData = navigator.connection && navigator.connection.saveData;
    if (reduce || saveData || window.location.hash) return;

    var seen = new IntersectionObserver(function (entries) {
      var step = 0;
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        var el = entry.target;
        seen.unobserve(el);
        el.style.transitionDelay = Math.min(step, 3) * 60 + "ms"; // small stagger, at most 3 steps
        step += 1;
        el.classList.add("is-in");
        window.setTimeout(function () { el.style.transitionDelay = ""; }, 700);
      });
    }, { threshold: 0, rootMargin: "0px 0px -8% 0px" });

    // Read first, then write: blocks already on screen stay as they are, the rest wait for the observer.
    var vh = window.innerHeight || document.documentElement.clientHeight;
    var below = [];
    items.forEach(function (el) {
      if (el.getBoundingClientRect().top < vh * 0.92) el.classList.add("is-in"); else below.push(el);
    });
    document.documentElement.classList.add("js-reveal");
    below.forEach(function (el) { seen.observe(el); });
  })();

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
