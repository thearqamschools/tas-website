/* The Arqam Schools admin: photo upload for news posts and events.
   Sends the chosen photos one at a time, with a progress count, so 20, 30 or more photos
   can be added at once without hitting the request size limit. Large photos are made
   smaller in the browser first (faster on mobile data); the server checks and resizes
   every photo again. Without JavaScript the form posts all photos in one request. */
(function () {
  "use strict";

  var MAX_SIDE = 2560;

  // Confirmation for "Remove" buttons.
  Array.prototype.forEach.call(document.querySelectorAll("form[data-confirm]"), function (form) {
    form.addEventListener("submit", function (e) {
      if (!window.confirm(form.getAttribute("data-confirm"))) e.preventDefault();
    });
  });

  var form = document.querySelector("[data-photo-upload]");
  if (!form || !window.fetch || !window.FormData || !window.Promise) return;
  var input = form.querySelector('input[type="file"]');
  var button = form.querySelector('button[type="submit"]');
  var box = form.querySelector("[data-upload-progress]");
  var bar = form.querySelector("[data-upload-bar]");
  var status = form.querySelector("[data-upload-status]");
  var errors = form.querySelector("[data-upload-errors]");
  var token = form.querySelector('input[name="__RequestVerificationToken"]');

  function addError(text) {
    var li = document.createElement("li");
    li.textContent = text;
    errors.appendChild(li);
  }

  // Makes a large JPEG/PNG smaller before upload. Falls back to the original file on any problem.
  function shrink(file) {
    if (!window.createImageBitmap || !/^image\/(jpeg|png)$/.test(file.type) || file.size < 1500000) return Promise.resolve(file);
    return createImageBitmap(file, { imageOrientation: "from-image" }).then(function (bmp) {
      var scale = Math.min(1, MAX_SIDE / Math.max(bmp.width, bmp.height));
      if (scale === 1 && file.size < 6000000) { bmp.close && bmp.close(); return file; }
      var canvas = document.createElement("canvas");
      canvas.width = Math.round(bmp.width * scale);
      canvas.height = Math.round(bmp.height * scale);
      var ctx = canvas.getContext("2d");
      ctx.fillStyle = "#fff";
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.drawImage(bmp, 0, 0, canvas.width, canvas.height);
      bmp.close && bmp.close();
      return new Promise(function (resolve) {
        canvas.toBlob(function (blob) {
          resolve(blob && blob.size < file.size ? new File([blob], file.name.replace(/\.(png|jpe?g)$/i, "") + ".jpg", { type: "image/jpeg" }) : file);
        }, "image/jpeg", 0.9);
      });
    }).catch(function () { return file; });
  }

  function upload(file) {
    return shrink(file).then(function (f) {
      var data = new FormData();
      data.append("__RequestVerificationToken", token.value);
      data.append("photos", f, f.name);
      return fetch(form.action, { method: "POST", body: data, credentials: "same-origin", headers: { Accept: "application/json" } });
    }).then(function (r) {
      return r.json().catch(function () { return { ok: false, message: "The server did not answer (status " + r.status + ")." }; })
        .then(function (j) { if (!r.ok || !j.ok) throw new Error(j.message || "The photo could not be added."); return j; });
    });
  }

  form.addEventListener("submit", function (e) {
    var files = Array.prototype.slice.call(input.files || []);
    if (files.length === 0) return; // let the server explain
    e.preventDefault();
    errors.textContent = "";
    box.hidden = false;
    button.disabled = true;
    input.disabled = true;
    var done = 0, ok = 0;
    bar.max = files.length;
    bar.value = 0;

    function next(i) {
      if (i >= files.length) {
        status.textContent = ok + " of " + files.length + " photos added." + (ok < files.length ? " The photos listed below were not added." : " Refreshing…");
        button.disabled = false;
        input.disabled = false;
        input.value = "";
        var url = window.location.pathname + "?added=" + ok + "#photos";
        if (ok === files.length) { window.location.assign(url); return; }
        if (ok > 0) {
          var again = document.createElement("a");
          again.href = url;
          again.textContent = "Show the added photos";
          status.appendChild(document.createTextNode(" "));
          status.appendChild(again);
        }
        return;
      }
      status.textContent = "Uploading photo " + (i + 1) + " of " + files.length + "…";
      upload(files[i]).then(function () { ok++; }, function (err) { addError(files[i].name + ": " + err.message); })
        .then(function () { done++; bar.value = done; next(i + 1); });
    }
    next(0);
  });

})();
