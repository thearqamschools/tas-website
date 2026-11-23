/*
 * Picture cards in the admin content forms (owner's request, 21 November 2026).
 * - "Choose file" shows the new picture at once (nothing is uploaded before Save).
 * - The X marks a picture to be removed (Undo brings it back); without JavaScript the tick box does the same.
 * - "Adjust" (homepage slides), like Facebook's "Reposition" (notes 48 and 52): drag the photo or use the arrow keys,
 *   zoom with the slider, Reset. x / y (0-100) is the point of the photo in the middle of the frame and z the zoom;
 *   the photo is placed with the same formula as the website (Content/site.css, "Adjust"), in a frame of the same
 *   shape as the website at the chosen screen size, so the frame shows what visitors see. The file is not changed.
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

    var note = card.querySelector('[data-img-note]');
    // The photo's size and the frame's size, in screen pixels: the photo just covers the frame, times the zoom.
    function geom() {
      var fw = frame.clientWidth, fh = frame.clientHeight;
      var ar = (img.naturalWidth && img.naturalHeight) ? img.naturalWidth / img.naturalHeight : fw / fh;
      var dw = num(z, 1) * Math.max(fw, fh * ar);
      return { fw: fw, fh: fh, ar: ar, dw: dw, dh: dw / ar };
    }
    // Keeps the chosen point where the photo still covers the whole frame (no empty edge), as the website does.
    function clampFocus() {
      if (!x.value) return;
      var g = geom();
      var cx = clamp(num(x, 50) / 100, g.fw / (2 * g.dw), 1 - g.fw / (2 * g.dw));
      var cy = clamp(num(y, 50) / 100, g.fh / (2 * g.dh), 1 - g.fh / (2 * g.dh));
      x.value = (cx * 100).toFixed(2); y.value = (cy * 100).toFixed(2);
    }
    function apply() {
      if (!adjustable || !img) return;
      var framed = !!x.value && !!img.naturalWidth;
      img.classList.toggle('is-framed', framed);
      if (framed) {
        img.style.setProperty('--cx', String(num(x, 50) / 100));
        img.style.setProperty('--cy', String(num(y, 50) / 100));
        img.style.setProperty('--z', String(num(z, 1)));
        img.style.setProperty('--ar', String(img.naturalWidth / img.naturalHeight));
      }
      if (note) note.textContent = x.value ? 'Position chosen with Adjust (saved when you press Save).' : 'Usual position (not adjusted).';
    }
    // When Adjust opens on a photo in its usual position, start from exactly what the website shows now.
    function startFromUsual() {
      if (x.value) return;
      var g = geom(), px = dx0 / 100, py = dy0 / 100;
      x.value = (((g.fw / 2) - (g.fw - g.dw) * px) / g.dw * 100).toFixed(2);
      y.value = (((g.fh / 2) - (g.fh - g.dh) * py) / g.dh * 100).toFixed(2);
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
    img.addEventListener('load', apply);
    apply();

    function openAdjust() {
      panel.hidden = false;
      card.classList.add('is-adjusting');
      frame.setAttribute('tabindex', '0');
      frame.setAttribute('role', 'application');
      frame.setAttribute('aria-label', 'Photo position. Drag, or use the arrow keys, to move the photo.');
      adjustBtn.setAttribute('aria-expanded', 'true');
      if (img.naturalWidth) { startFromUsual(); apply(); }
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
      x.value = ''; y.value = ''; z.value = ''; zoom.value = '1'; apply();   // back to the usual position
    });
    zoom.addEventListener('input', function () {
      var v = clamp(parseFloat(zoom.value) || 1, 1, 3);
      startFromUsual();
      z.value = v === 1 ? '' : v.toFixed(2);
      clampFocus(); apply();
    });
    // Screen sizes for the computer photo: the frame takes the shape of the website's top photo on that screen.
    var sizes = card.querySelectorAll('[data-img-size]');
    for (var k = 0; k < sizes.length; k++) sizes[k].addEventListener('click', function () {
      var wh = this.getAttribute('data-img-size').split('x');
      frame.style.aspectRatio = wh[0] + ' / ' + wh[1];
      for (var j = 0; j < sizes.length; j++) sizes[j].setAttribute('aria-pressed', sizes[j] === this ? 'true' : 'false');
    });

    // Facebook-style dragging: the photo follows the pointer one to one.
    function moveBy(px, py) {
      startFromUsual();
      var g = geom();
      x.value = ((num(x, 50) / 100 - px / g.dw) * 100).toFixed(2);
      y.value = ((num(y, 50) / 100 - py / g.dh) * 100).toFixed(2);
      clampFocus(); apply();
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
      var step = e.shiftKey ? 20 : 5;   // pixels; the arrow moves the photo that way
      if (e.key === 'ArrowLeft') moveBy(-step, 0);
      else if (e.key === 'ArrowRight') moveBy(step, 0);
      else if (e.key === 'ArrowUp') moveBy(0, -step);
      else if (e.key === 'ArrowDown') moveBy(0, step);
      else if (e.key === 'Escape') { closeAdjust(); adjustBtn.focus(); return; }
      else return;
      e.preventDefault();
    });
  }

  var cards = document.querySelectorAll('[data-img-card]');
  for (var i = 0; i < cards.length; i++) setup(cards[i]);
})();
