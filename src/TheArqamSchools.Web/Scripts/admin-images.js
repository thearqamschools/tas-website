/*
 * Picture cards in the admin content forms (owner's request, 21 November 2026).
 * - "Choose file" shows the new picture at once (nothing is uploaded before Save).
 * - The X marks a picture to be removed (Undo brings it back); without JavaScript the tick box does the same.
 * - "Adjust" (homepage slides): drag the photo or use the arrow keys to move it, zoom with the slider, Reset.
 *   The frame has the same shape as on the website, and the same CSS (object-position + scale) is used, so what you
 *   see here is what visitors see. Only the position and zoom are saved (x/y/z fields); the file is not changed.
 */
(function () {
  'use strict';

  function clamp(v, min, max) { return Math.min(max, Math.max(min, v)); }
  function num(input, fallback) { var v = parseFloat(input && input.value); return isNaN(v) ? fallback : v; }

  function setup(card) {
    var img = card.querySelector('[data-img-preview]');
    var none = card.querySelector('[data-img-none]');
    var frame = card.querySelector('[data-img-frame]');
    var file = card.querySelector('[data-img-file]');
    var name = card.querySelector('[data-img-filename]');
    var x = card.querySelector('[data-img-x]');
    var y = card.querySelector('[data-img-y]');
    var z = card.querySelector('[data-img-z]');
    var zoom = card.querySelector('[data-img-zoom]');
    var adjustBtn = card.querySelector('[data-img-adjust]');
    var panel = card.querySelector('[data-img-adjust-panel]');
    var removeBtn = card.querySelector('[data-img-remove]');
    var removedBox = card.querySelector('[data-img-removed]');
    var removeCheck = card.querySelector('[data-img-remove-box]');
    var nojs = card.querySelector('[data-img-nojs]');
    var adjustable = !!x;
    var dx0 = x ? parseFloat(x.getAttribute('data-default')) : 50;
    var dy0 = y ? parseFloat(y.getAttribute('data-default')) : 50;
    var originalSrc = img && img.getAttribute('src');
    var objectUrl = null;

    // The JavaScript X replaces the tick box.
    if (nojs) nojs.hidden = true;
    if (removeBtn) removeBtn.hidden = false;

    function apply() {
      if (!adjustable || !img) return;
      var fx = num(x, dx0), fy = num(y, dy0), zz = num(z, 1);
      img.style.objectPosition = fx + '% ' + fy + '%';
      img.style.transformOrigin = fx + '% ' + fy + '%';
      img.style.transform = 'scale(' + zz + ')';
    }

    function setRemoved(on) {
      if (removeCheck) removeCheck.checked = on;
      if (removedBox) removedBox.hidden = !on;
      card.classList.toggle('is-removed', on);
      if (removeBtn) removeBtn.hidden = on;
      if (on) closeAdjust();
    }

    if (removeBtn) removeBtn.addEventListener('click', function () { setRemoved(true); });
    var undo = card.querySelector('[data-img-undo]');
    if (undo) undo.addEventListener('click', function () { setRemoved(false); removeBtn.focus(); });

    if (file) file.addEventListener('change', function () {
      var f = file.files && file.files[0];
      if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
      if (!f) {
        if (name) name.textContent = '';
        if (originalSrc) { img.src = originalSrc; img.hidden = false; if (none) none.hidden = true; }
        else { img.hidden = true; if (none) none.hidden = false; if (adjustBtn) adjustBtn.hidden = true; }
        return;
      }
      if (name) name.textContent = f.name + ' (new, saved when you press Save)';
      objectUrl = URL.createObjectURL(f);
      img.src = objectUrl;
      img.alt = 'New picture: ' + f.name;
      img.hidden = false;
      if (none) none.hidden = true;
      card.classList.remove('is-empty');
      setRemoved(false);
      // A new photo starts from the website's usual position.
      if (adjustable) { x.value = ''; y.value = ''; z.value = ''; if (zoom) zoom.value = '1'; apply(); }
      if (adjustBtn) adjustBtn.hidden = false;
    });

    if (!adjustable) return;
    apply();

    function openAdjust() {
      panel.hidden = false;
      card.classList.add('is-adjusting');
      frame.setAttribute('tabindex', '0');
      frame.setAttribute('role', 'application');
      frame.setAttribute('aria-label', 'Photo position. Use the arrow keys to move the photo.');
      adjustBtn.setAttribute('aria-expanded', 'true');
      frame.focus();
    }
    function closeAdjust() {
      if (!panel || panel.hidden) return;
      panel.hidden = true;
      card.classList.remove('is-adjusting');
      frame.removeAttribute('tabindex');
      frame.removeAttribute('role');
      frame.removeAttribute('aria-label');
      adjustBtn.setAttribute('aria-expanded', 'false');
    }
    adjustBtn.addEventListener('click', function () { if (panel.hidden) openAdjust(); else { closeAdjust(); adjustBtn.focus(); } });
    card.querySelector('[data-img-done]').addEventListener('click', function () { closeAdjust(); adjustBtn.focus(); });
    card.querySelector('[data-img-reset]').addEventListener('click', function () {
      x.value = ''; y.value = ''; z.value = ''; zoom.value = '1'; apply();
    });
    zoom.addEventListener('input', function () {
      var v = clamp(parseFloat(zoom.value) || 1, 1, 3);
      z.value = v === 1 ? '' : v.toFixed(2);
      if (!x.value) x.value = String(dx0);
      if (!y.value) y.value = String(dy0);
      apply();
    });

    // How far the picture moves on screen for one percent of fx (or fy): object-position moves the part that does
    // not fit (cover), and the zoom around the same point moves it by (1 - zoom). Both are negative, so dragging
    // right lowers fx. Returns 0 when the picture cannot move that way.
    function rate(frameSize, shownSize, zz) {
      return (frameSize * (1 - zz) + zz * (frameSize - shownSize)) / 100;
    }
    function shown() {
      var fw = frame.clientWidth, fh = frame.clientHeight;
      var nw = img.naturalWidth || fw, nh = img.naturalHeight || fh;
      var s = Math.max(fw / nw, fh / nh);
      return { fw: fw, fh: fh, w: nw * s, h: nh * s };
    }
    function moveBy(px, py) {
      var m = shown(), zz = num(z, 1);
      var rx = rate(m.fw, m.w, zz), ry = rate(m.fh, m.h, zz);
      var fx = num(x, dx0), fy = num(y, dy0);
      if (Math.abs(rx) > 0.0001) fx = clamp(fx + px / rx, 0, 100);
      if (Math.abs(ry) > 0.0001) fy = clamp(fy + py / ry, 0, 100);
      x.value = fx.toFixed(1); y.value = fy.toFixed(1);
      apply();
    }

    var dragging = null;
    frame.addEventListener('pointerdown', function (e) {
      if (panel.hidden || card.classList.contains('is-removed')) return;
      dragging = { x: e.clientX, y: e.clientY };
      frame.setPointerCapture(e.pointerId);
      e.preventDefault();
    });
    frame.addEventListener('pointermove', function (e) {
      if (!dragging) return;
      moveBy(e.clientX - dragging.x, e.clientY - dragging.y);
      dragging = { x: e.clientX, y: e.clientY };
    });
    function stop() { dragging = null; }
    frame.addEventListener('pointerup', stop);
    frame.addEventListener('pointercancel', stop);
    frame.addEventListener('keydown', function (e) {
      if (panel.hidden) return;
      var step = e.shiftKey ? 2 : 0.5, fx = num(x, dx0), fy = num(y, dy0), done = true;
      // Arrow keys move the photo in the arrow's direction (so the point shown moves the other way).
      if (e.key === 'ArrowLeft') fx = clamp(fx + step, 0, 100);
      else if (e.key === 'ArrowRight') fx = clamp(fx - step, 0, 100);
      else if (e.key === 'ArrowUp') fy = clamp(fy + step, 0, 100);
      else if (e.key === 'ArrowDown') fy = clamp(fy - step, 0, 100);
      else if (e.key === 'Escape') { done = false; closeAdjust(); adjustBtn.focus(); }
      else done = false;
      if (done) { e.preventDefault(); x.value = fx.toFixed(1); y.value = fy.toFixed(1); apply(); }
    });
  }

  var cards = document.querySelectorAll('[data-img-card]');
  for (var i = 0; i < cards.length; i++) setup(cards[i]);
})();
