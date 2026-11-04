/* Texture-backed nine-slice renderer. Only decoration is painted; every label,
 * task, button and input remains a real accessible DOM control. Atlas coordinates
 * are measured in the 1107 × 1421 reference design, independent of DPI/window size. */
(() => {
  const canvas = document.createElement("canvas");
  canvas.id = "skin-canvas";
  canvas.setAttribute("aria-hidden", "true");
  document.body.prepend(canvas);
  const ctx = canvas.getContext("2d");
  const specs = {
    oak: {
      frame: [0, 0, 1107, 1421],
      frameCuts: [30, 176, 30, 140],
      panel: [28, 578, 1016, 264],
      cuts: [40, 35, 40, 35],
      controls: [18, 176, 1073, 100],
      controlCuts: [28, 22, 28, 22],
      green: [71, 447, 188, 90],
      stone: [277, 447, 185, 90],
      background: "#a76628",
      line: "#88522b",
      track: "#67320f",
      edge: "#391c0b",
    },
    slate: {
      frame: [0, 0, 1107, 1421],
      frameCuts: [50, 174, 50, 170],
      panel: [34, 582, 1006, 266],
      cuts: [40, 35, 40, 35],
      controls: [28, 175, 1047, 104],
      controlCuts: [28, 22, 28, 22],
      green: [73, 450, 193, 94],
      stone: [275, 450, 195, 99],
      background: "#111720",
      line: "#aeb5c0",
      track: "#060d14",
      edge: "#49505e",
    },
  };
  const images = {};
  const ready = Promise.all(
    Object.keys(specs).map(
      (key) =>
        new Promise((resolve, reject) => {
          const image = new Image();
          image.onload = () => {
            images[key] = image;
            resolve();
          };
          image.onerror = () =>
            reject(new Error("Skin material failed to load: " + key));
          image.src = `assets/${key}-atlas.png`;
        }),
    ),
  );
  let queued = false;
  function rect(node) {
    const r = node.getBoundingClientRect();
    return [r.x, r.y, r.width, r.height];
  }
  function region(image, src, dst) {
    // Source assets retain the same normalized geometry even if generated at another resolution.
    const sx = image.naturalWidth / 1107,
      sy = image.naturalHeight / 1421;
    ctx.drawImage(
      image,
      src[0] * sx,
      src[1] * sy,
      src[2] * sx,
      src[3] * sy,
      ...dst,
    );
  }
  function nine(image, src, dst, cuts, scale, center = true) {
    const [x, y, w, h] = src,
      [dx, dy, dw, dh] = dst,
      [l, t, r, b] = cuts;
    const ex = [x, x + l, x + w - r, x + w],
      ey = [y, y + t, y + h - b, y + h];
    const ox = [dx, dx + l * scale, dx + dw - r * scale, dx + dw],
      oy = [dy, dy + t * scale, dy + dh - b * scale, dy + dh];
    for (let row = 0; row < 3; row++)
      for (let col = 0; col < 3; col++) {
        if (!center && row === 1 && col === 1) continue;
        const a = [
          ex[col],
          ey[row],
          ex[col + 1] - ex[col],
          ey[row + 1] - ey[row],
        ];
        const d = [
          ox[col],
          oy[row],
          ox[col + 1] - ox[col],
          oy[row + 1] - oy[row],
        ];
        if (d[2] > 0 && d[3] > 0) region(image, a, d);
      }
  }
  function fill(color, x, y, w, h) {
    ctx.fillStyle = color;
    ctx.fillRect(x, y, w, h);
  }
  function draw() {
    queued = false;
    const key = document.body.dataset.skin === "slate" ? "slate" : "oak",
      image = images[key];
    if (!image) return;
    const s = specs[key],
      w = innerWidth,
      h = innerHeight,
      u = w / 1107,
      dpr = devicePixelRatio || 1;
    canvas.width = Math.round(w * dpr);
    canvas.height = Math.round(h * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.imageSmoothingEnabled = false;
    fill(s.background, 0, 0, w, h);
    if (document.body.classList.contains("folded")) {
      region(image, [0, 0, 1107, 176], [0, 0, w, 176 * u]);
      return;
    }
    nine(image, s.frame, [0, 0, w, h], s.frameCuts, u, false);
    const controls = document.getElementById("controls");
    nine(image, s.controls, rect(controls), s.controlCuts, u);
    const input = document.getElementById("opacity"),
      [rx, ry, rw, rh] = rect(input),
      th = 26 * u;
    fill(s.edge, rx, ry + (rh - th) / 2, rw, th);
    fill(
      s.track,
      rx + 5 * u,
      ry + (rh - th) / 2 + 5 * u,
      rw - 10 * u,
      th - 10 * u,
    );
    if (key === "oak") {
      fill(
        "#9b5c21",
        rx + 6 * u,
        ry + (rh - th) / 2 + 5 * u,
        rw - 12 * u,
        4 * u,
      );
    }
    const sz = 48 * u,
      ratio = (Number(input.value) - 40) / 60,
      tx = rx + ratio * (rw - sz),
      ty = ry + (rh - sz) / 2;
    fill(key === "oak" ? "#092a0e" : "#2b0c09", tx, ty, sz, sz);
    const bright = key === "oak" ? "#b0ff83" : "#ff996b",
      mid = key === "oak" ? "#10bd2b" : "#d72b15",
      shade = key === "oak" ? "#086a1b" : "#7a150c";
    fill(mid, tx + 5 * u, ty + 5 * u, sz - 10 * u, sz - 10 * u);
    fill(bright, tx + 5 * u, ty + 5 * u, sz - 13 * u, 6 * u);
    fill(bright, tx + 5 * u, ty + 10 * u, 6 * u, 19 * u);
    fill(shade, tx + 11 * u, ty + sz - 12 * u, sz - 16 * u, 7 * u);
    fill(shade, tx + sz - 12 * u, ty + 11 * u, 7 * u, sz - 16 * u);
    if (key === "oak") {
      fill("#d6ffb4", tx + 12 * u, ty + 12 * u, 9 * u, 9 * u);
      fill("#44e153", tx + 20 * u, ty + 19 * u, 12 * u, 12 * u);
    }
    const list = document.getElementById("tasklist"),
      [lx, ly, lw, lh] = rect(list);
    ctx.save();
    ctx.beginPath();
    ctx.rect(lx, ly, lw, lh);
    ctx.clip();
    for (const card of list.querySelectorAll(".task")) {
      const cr = rect(card);
      if (cr[1] > ly + lh || cr[1] + cr[3] < ly) continue;
      nine(image, s.panel, cr, s.cuts, u);
      const actions = card.querySelector(".task-actions"),
        [ax, ay, aw] = rect(actions);
      for (let x = ax; x < ax + aw; x += 23 * u)
        fill(s.line, x, ay, Math.min(13 * u, ax + aw - x), 5 * u);
      for (const b of actions.querySelectorAll(".primary-action")) {
        const br = rect(b);
        nine(
          image,
          b.classList.contains("green") ? s.green : s.stone,
          br,
          [25, 20, 25, 20],
          u,
        );
      }
    }
    ctx.restore();
    const bar = document.getElementById("task-scrollbar"),
      [bx, by, bw, bh] = rect(bar);
    const metrics = scrollMetrics();
    fill(key === "oak" ? "#4c2b12" : "#0c111b", bx, by, bw, bh);
    fill(key === "oak" ? "#ae7939" : "#7c8592", bx, by, 3 * u, bh);
    fill(key === "oak" ? "#dfb36b" : "#3a4351", bx + bw - 3 * u, by, 3 * u, bh);
    const thumb = key === "oak" ? [1052, 757, 24, 454] : [1050, 709, 26, 96];
    nine(
      image,
      thumb,
      [bx + 4 * u, by + metrics.top, bw - 8 * u, metrics.height],
      [5, 12, 5, 12],
      u,
    );
    bar.setAttribute("aria-valuenow", String(Math.round(metrics.ratio * 100)));
    if (document.body.classList.contains("busy") && key === "slate")
      fill("#e6ad32", 95 * u, h - 112 * u, 24 * u, 24 * u);
  }
  function scrollMetrics() {
    const list = document.getElementById("tasklist"),
      bar = document.getElementById("task-scrollbar");
    const range = Math.max(0, list.scrollHeight - list.clientHeight),
      height = Math.min(
        bar.clientHeight,
        Math.max(
          (50 * innerWidth) / 1107,
          (bar.clientHeight * list.clientHeight) /
            Math.max(1, list.scrollHeight),
        ),
      );
    const ratio = range ? list.scrollTop / range : 0;
    return {
      range,
      height,
      ratio,
      top: ratio * (bar.clientHeight - height),
      travel: bar.clientHeight - height,
    };
  }
  const bar = document.getElementById("task-scrollbar"),
    list = document.getElementById("tasklist");
  let grab = null;
  bar.onpointerdown = (e) => {
    const m = scrollMetrics(),
      y = e.clientY - bar.getBoundingClientRect().top;
    grab = y >= m.top && y <= m.top + m.height ? y - m.top : m.height / 2;
    bar.setPointerCapture(e.pointerId);
    if (m.travel > 0)
      list.scrollTop =
        Math.max(0, Math.min(1, (y - grab) / m.travel)) * m.range;
  };
  bar.onpointermove = (e) => {
    if (grab === null) return;
    const m = scrollMetrics();
    if (m.travel > 0)
      list.scrollTop =
        Math.max(
          0,
          Math.min(
            1,
            (e.clientY - bar.getBoundingClientRect().top - grab) / m.travel,
          ),
        ) * m.range;
  };
  bar.onpointerup = bar.onlostpointercapture = () => {
    grab = null;
  };
  bar.onkeydown = (e) => {
    const amounts = {
      ArrowDown: 50,
      ArrowUp: -50,
      PageDown: list.clientHeight,
      PageUp: -list.clientHeight,
      Home: -list.scrollHeight,
      End: list.scrollHeight,
    };
    if (e.key in amounts) {
      e.preventDefault();
      list.scrollTop += amounts[e.key];
    }
  };
  function schedule() {
    if (!queued) {
      queued = true;
      requestAnimationFrame(draw);
    }
  }
  window.SkinRenderer = {
    ready,
    paint: async () => {
      await ready;
      draw();
    },
    schedule,
  };
  ready.then(schedule).catch((error) => {
    document.body.dataset.materialError = "true";
    console.error(error);
  });
  new ResizeObserver(schedule).observe(document.body);
  new MutationObserver(schedule).observe(document.body, {
    subtree: true,
    childList: true,
    attributes: true,
    attributeFilter: ["data-skin", "class"],
  });
  document
    .getElementById("tasklist")
    .addEventListener("scroll", schedule, { passive: true });
  document.getElementById("opacity").addEventListener("input", schedule);
  window.addEventListener("resize", schedule);
})();
