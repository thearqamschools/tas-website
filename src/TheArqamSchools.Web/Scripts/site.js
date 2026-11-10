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

  // News & Events filter buttons: show one category without reloading. The links still work without JavaScript.
  (function () {
    var buttons = document.querySelectorAll("[data-news-filter]");
    if (!buttons.length) return;
    var items = document.querySelectorAll("[data-news-category]");
    var status = document.querySelector("[data-news-status]");
    buttons.forEach(function (b) {
      b.addEventListener("click", function (e) {
        e.preventDefault();
        var key = b.getAttribute("data-news-filter");
        var shown = 0;
        items.forEach(function (li) {
          var match = !key || li.getAttribute("data-news-category") === key;
          li.hidden = !match;
          if (match) shown++;
        });
        buttons.forEach(function (x) { x.setAttribute("aria-pressed", x === b ? "true" : "false"); });
        if (status) status.textContent = shown + (shown === 1 ? " item" : " items") + " shown";
        if (window.history && history.replaceState) history.replaceState(null, "", b.getAttribute("href"));
      });
    });
  })();

  // Header motto (Arabic): sized from the width of the name above it and centred under it.
  function fitMotto() {
    document.querySelectorAll(".site-header .brand-text").forEach(function (t) {
      var name = t.querySelector(".brand-name"), motto = t.querySelector('.brand-tagline[lang="ar"]');
      if (!name || !motto || motto.offsetParent === null) return;
      motto.style.width = ""; motto.style.fontSize = "";   // start from the CSS size each time
      var target = name.getBoundingClientRect().width;
      var size = parseFloat(getComputedStyle(motto).fontSize) || 16;
      for (var i = 0; i < 4; i++) {
        var w = motto.getBoundingClientRect().width;
        if (!w || Math.abs(w - target) < 0.5) break;
        size = size * target / w;
        motto.style.fontSize = size + "px";
      }
      // Owner: about 1 point smaller than full width, centred under the name.
      motto.style.fontSize = Math.max(size - 1.35, 10) + "px";
      motto.style.width = target + "px";
      // Logo as tall as the name and the motto together (top of the name to the bottom of the motto).
      var logo = t.parentNode && t.parentNode.querySelector("img");
      if (logo) {
        logo.style.width = ""; logo.style.height = "";
        var top = name.getBoundingClientRect().top, bottom = motto.getBoundingClientRect().bottom;
        var h = Math.round(bottom - top);
        if (h > 30 && h < 120) { logo.style.width = h + "px"; logo.style.height = h + "px"; }
      }
    });
  }
  // Footer motto (Arabic): centred under the logo and name, as wide as both together.
  function fitFooterMotto() {
    document.querySelectorAll(".footer-identity").forEach(function (box) {
      var brand = box.querySelector(".brand"), motto = box.querySelector('.footer-tagline[lang="ar"]');
      if (!brand || !motto || motto.offsetParent === null) return;
      motto.style.fontSize = ""; motto.style.width = "";
      var target = brand.getBoundingClientRect().width;
      var size = parseFloat(getComputedStyle(motto).fontSize) || 20;
      for (var i = 0; i < 4; i++) {
        var r = document.createRange(); r.selectNodeContents(motto);
        var w = r.getBoundingClientRect().width;
        if (!w || Math.abs(w - target) < 0.5) break;
        size = size * target / w;
        motto.style.fontSize = size + "px";
      }
      motto.style.width = target + "px";
    });
  }
  fitMotto(); fitFooterMotto();
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { fitMotto(); fitFooterMotto(); });
  window.addEventListener("resize", fitFooterMotto);
  window.addEventListener("resize", function () {
    fitMotto();
  });

  // News and event photo gallery: main photo, thumbnail strip and a full-screen viewer.
  // Without JavaScript every thumbnail is a link to the large photo.
  document.querySelectorAll("[data-gallery]").forEach(function (g) {
    var thumbs = Array.prototype.slice.call(g.querySelectorAll("[data-gallery-thumb]"));
    var main = g.querySelector("[data-gallery-main]");
    var open = g.querySelector("[data-gallery-open]");
    var count = g.querySelector("[data-gallery-count]");
    var prev = g.querySelector("[data-gallery-prev]");
    var next = g.querySelector("[data-gallery-next]");
    var strip = g.querySelector("[data-gallery-thumbs]");
    var photos = thumbs.length ? thumbs.map(function (t) {
      return { src: t.getAttribute("data-large"), alt: t.getAttribute("data-alt") || "", w: t.getAttribute("data-w"), h: t.getAttribute("data-h") };
    }) : [{ src: main.getAttribute("src"), alt: main.getAttribute("alt") || "", w: main.getAttribute("width"), h: main.getAttribute("height") }];
    var n = photos.length, current = 0, dialog = null, big = null, bigCount = null;
    var reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    function preload(i) { if (n > 1) { var im = new Image(); im.src = photos[(i + n) % n].src; } }

    function show(i, fromStrip) {
      current = (i + n) % n;
      var p = photos[current];
      main.classList.remove("is-in");
      main.src = p.src; main.alt = p.alt;
      if (p.w && p.h) { main.width = p.w; main.height = p.h; }
      void main.offsetWidth; main.classList.add("is-in");
      open.href = p.src;
      open.setAttribute("aria-label", "Open photo " + (current + 1) + " of " + n + " full screen");
      if (count) count.textContent = (current + 1) + " / " + n;
      thumbs.forEach(function (t, k) { if (k === current) t.setAttribute("aria-current", "true"); else t.removeAttribute("aria-current"); });
      if (strip && thumbs[current] && !fromStrip) {
        var t = thumbs[current].parentNode;
        strip.scrollTo({ left: t.offsetLeft - (strip.clientWidth - t.offsetWidth) / 2, behavior: reduce ? "auto" : "smooth" });
      }
      if (big) { big.src = p.src; big.alt = p.alt; bigCount.textContent = (current + 1) + " / " + n; }
      preload(current + 1); preload(current - 1);
    }

    thumbs.forEach(function (t, k) {
      t.addEventListener("click", function (e) { e.preventDefault(); show(k, true); });
    });
    if (prev) { prev.hidden = false; prev.addEventListener("click", function () { show(current - 1); }); }
    if (next) { next.hidden = false; next.addEventListener("click", function () { show(current + 1); }); }
    g.addEventListener("keydown", function (e) {
      if (dialog && dialog.open) return;
      if (e.key === "ArrowLeft") { show(current - 1); } else if (e.key === "ArrowRight") { show(current + 1); }
    });
    swipe(g.querySelector(".gallery-stage"), function (d) { show(current + d); });

    function swipe(el, go) {
      if (!el || n < 2) return;
      var x0 = null, y0 = 0;
      el.addEventListener("touchstart", function (e) { x0 = e.touches[0].clientX; y0 = e.touches[0].clientY; }, { passive: true });
      el.addEventListener("touchend", function (e) {
        if (x0 === null) return;
        var dx = e.changedTouches[0].clientX - x0, dy = e.changedTouches[0].clientY - y0;
        x0 = null;
        if (Math.abs(dx) > 40 && Math.abs(dx) > Math.abs(dy) * 1.5) go(dx < 0 ? 1 : -1);
      }, { passive: true });
    }

    function build() {
      dialog = document.createElement("dialog");
      dialog.className = "lightbox";
      dialog.setAttribute("aria-label", "Photos");
      dialog.innerHTML =
        '<div class="lightbox-bar"><p class="lightbox-count" aria-live="polite"></p>' +
        '<button type="button" class="lightbox-btn lightbox-close" aria-label="Close">&times;</button></div>' +
        '<div class="lightbox-stage"><img class="lightbox-img" alt="" /></div>' +
        (n > 1 ? '<button type="button" class="lightbox-btn lightbox-prev" aria-label="Previous photo"></button>' +
                 '<button type="button" class="lightbox-btn lightbox-next" aria-label="Next photo"></button>' : "");
      var arrow = next ? next.innerHTML : "";
      if (n > 1) {
        dialog.querySelector(".lightbox-prev").innerHTML = arrow;
        dialog.querySelector(".lightbox-next").innerHTML = arrow;
        dialog.querySelector(".lightbox-prev").addEventListener("click", function () { show(current - 1); });
        dialog.querySelector(".lightbox-next").addEventListener("click", function () { show(current + 1); });
      }
      big = dialog.querySelector(".lightbox-img");
      bigCount = dialog.querySelector(".lightbox-count");
      dialog.querySelector(".lightbox-close").addEventListener("click", function () { dialog.close(); });
      dialog.addEventListener("click", function (e) { if (e.target === dialog || e.target.classList.contains("lightbox-stage")) dialog.close(); });
      dialog.addEventListener("keydown", function (e) {
        if (e.key === "ArrowLeft") { e.preventDefault(); show(current - 1); }
        else if (e.key === "ArrowRight") { e.preventDefault(); show(current + 1); }
      });
      dialog.addEventListener("close", function () { document.documentElement.classList.remove("lightbox-open"); open.focus(); });
      swipe(dialog.querySelector(".lightbox-stage"), function (d) { show(current + d); });
      document.body.appendChild(dialog);
    }

    open.addEventListener("click", function (e) {
      if (typeof HTMLDialogElement !== "function") return; // old browsers: open the photo itself
      e.preventDefault();
      if (!dialog) build();
      show(current, true);
      document.documentElement.classList.add("lightbox-open");
      dialog.showModal();
    });
  });

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
