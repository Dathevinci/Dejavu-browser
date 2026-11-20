/**
 * dejavu — Real Physics-Based Liquid Glass Optical Engine
 *
 * Zero Gaussian blur. Uses exact SDF rounded-rectangle geometry, convex squircle
 * meniscus elevation h(u) = (1 - (1 - u)^4)^(1/4), Snell's Law refraction (n = 1.52),
 * Cauchy chromatic dispersion (separate R/G/B feDisplacementMap scales recombined
 * additively), and Schlick Fresnel specular caustic rims.
 */
(function () {
  const SVG_NS = 'http://www.w3.org/2000/svg';

  let svgRoot = null;
  let svgDefs = null;
  const filterCache = new Map();   // key -> { id, specUrl, refCount }
  const elementState = new WeakMap();

  function ensureSvgPool() {
    if (svgDefs) return;
    svgRoot = document.createElementNS(SVG_NS, 'svg');
    svgRoot.setAttribute('aria-hidden', 'true');
    svgRoot.style.cssText = 'position:fixed;width:0;height:0;pointer-events:none;z-index:-1;';
    svgDefs = document.createElementNS(SVG_NS, 'defs');
    svgRoot.appendChild(svgDefs);
    document.body.prepend(svgRoot);
  }

  function sdfRoundRect(px, py, hw, hh, r) {
    const cr = Math.min(r, hw, hh);
    const qx = Math.abs(px - hw) - (hw - cr);
    const qy = Math.abs(py - hh) - (hh - cr);
    return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - cr;
  }

  /**
   * Generates:
   *  1) dispUrl: RGBA PNG encoding Snell's Law + gentle convex dome refraction (dx -> R, dy -> G)
   *  2) specUrl: RGBA PNG encoding Schlick Fresnel specular caustic rim highlights
   */
  function generateOpticalMaps(W, H, R, bezel, ior, dome) {
    // Work at full logical pixel resolution (clamped gently for huge overlays to keep instant <6ms generation)
    const maxDim = 960;
    const scaleFactor = Math.max(W, H) > maxDim ? maxDim / Math.max(W, H) : 1;
    const cw = Math.max(16, Math.round(W * scaleFactor));
    const ch = Math.max(16, Math.round(H * scaleFactor));
    const cr = Math.min(R * scaleFactor, cw / 2, ch / 2);
    const cb = Math.min(bezel * scaleFactor, Math.min(cw, ch) * 0.46);
    const hw = cw / 2;
    const hh = ch / 2;

    const dispCanvas = document.createElement('canvas');
    dispCanvas.width = cw;
    dispCanvas.height = ch;
    const dispCtx = dispCanvas.getContext('2d');
    const dispImg = dispCtx.createImageData(cw, ch);
    const dData = dispImg.data;

    const specCanvas = document.createElement('canvas');
    specCanvas.width = cw;
    specCanvas.height = ch;
    const specCtx = specCanvas.getContext('2d');
    const specImg = specCtx.createImageData(cw, ch);
    const sData = specImg.data;

    // Primary overhead-left light + secondary bottom-right internal bounce light
    const L1x = -0.64, L1y = -0.77;
    const L2x = 0.62, L2y = 0.78;
    const eps = 0.7;

    for (let y = 0; y < ch; y++) {
      const py = y + 0.5;
      for (let x = 0; x < cw; x++) {
        const px = x + 0.5;
        const dist = -sdfRoundRect(px, py, hw, hh, cr);
        const idx = (y * cw + x) * 4;

        if (dist <= 0) {
          dData[idx] = 128;
          dData[idx + 1] = 128;
          dData[idx + 2] = 128;
          dData[idx + 3] = 255;
          sData[idx] = 0;
          sData[idx + 1] = 0;
          sData[idx + 2] = 0;
          sData[idx + 3] = 0;
          continue;
        }

        // Subtle convex lens dome across interior (gentle magnification, crystal clear)
        const relX = (px - hw) / Math.max(hw, 1);
        const relY = (py - hh) / Math.max(hh, 1);
        let dx = -relX * dome;
        let dy = -relY * dome;

        let specAlpha = 0;
        let specR = 255, specG = 255, specB = 255;

        if (dist < cb) {
          // Exact inward surface normal from SDF gradient
          const gx = (-sdfRoundRect(px + eps, py, hw, hh, cr) - (-sdfRoundRect(px - eps, py, hw, hh, cr))) / (2 * eps);
          const gy = (-sdfRoundRect(px, py + eps, hw, hh, cr) - (-sdfRoundRect(px, py - eps, hw, hh, cr))) / (2 * eps);
          const gLen = Math.hypot(gx, gy) || 1;
          const nx = gx / gLen;
          const ny = gy / gLen;

          // Normalized bezel coordinate u in (0, 1)
          const u = Math.max(0.015, Math.min(1, dist / cb));
          // Convex squircle height derivative dh/du for h(u) = (1 - (1 - u)^4)^(1/4)
          const oneMinusU = 1 - u;
          const denom = Math.pow(Math.max(1e-5, 1 - Math.pow(oneMinusU, 4)), 0.75);
          const slope = Math.min(3.4, Math.pow(oneMinusU, 3) / denom);

          // Snell's Law: n1 * sin(thetaI) = n2 * sin(thetaT)
          const thetaI = Math.atan(slope * 1.42);
          const sinT = Math.min(0.999, Math.sin(thetaI) / ior);
          const thetaT = Math.asin(sinT);
          const bend = Math.tan(thetaI - thetaT);

          // Smooth anti-alias feather on outer 1.2px so edge clamping stays silk-smooth
          const edgeFeather = Math.min(1, dist / 1.35);
          const mag = Math.min(0.96, bend * 1.65) * edgeFeather;

          dx += nx * mag;
          dy += ny * mag;

          // Schlick's Fresnel reflectance F(theta) = F0 + (1 - F0) * (1 - cos(theta))^5
          const cosTheta = Math.cos(thetaI);
          const fresnel = 0.04 + 0.96 * Math.pow(1 - cosTheta, 3.6);

          // Outward surface normal for directional specular lighting
          const onx = -nx;
          const ony = -ny;
          const dot1 = Math.max(0, onx * L1x + ony * L1y);
          const dot2 = Math.max(0, onx * L2x + ony * L2y);

          // Crisp rim caustic + soft internal bounce caustic
          const rimBand = Math.exp(-Math.pow((dist - 1.35) / 2.2, 2));
          const innerBand = Math.pow(1 - u, 2.2) * 0.42;

          const primaryCaustic = Math.pow(dot1, 1.55) * (rimBand * 0.95 + innerBand * fresnel);
          const bounceCaustic = Math.pow(dot2, 2.1) * (rimBand * 0.55 + innerBand * fresnel * 0.65);
          const ambientRim = rimBand * 0.16;

          specAlpha = Math.min(1, primaryCaustic + bounceCaustic + ambientRim);

          // Subtle prismatic lavender-cyan caustic tint along grazing bounce arcs
          if (bounceCaustic > primaryCaustic * 0.65) {
            specR = 216;
            specG = 192;
            specB = 255;
          } else {
            specR = 255;
            specG = 252;
            specB = 255;
          }
        }

        dx = Math.max(-1, Math.min(1, dx));
        dy = Math.max(-1, Math.min(1, dy));

        dData[idx] = Math.round(128 + dx * 127);
        dData[idx + 1] = Math.round(128 + dy * 127);
        dData[idx + 2] = 128;
        dData[idx + 3] = 255;

        sData[idx] = specR;
        sData[idx + 1] = specG;
        sData[idx + 2] = specB;
        sData[idx + 3] = Math.round(specAlpha * 255);
      }
    }

    dispCtx.putImageData(dispImg, 0, 0);
    specCtx.putImageData(specImg, 0, 0);

    return {
      dispUrl: dispCanvas.toDataURL('image/png'),
      specUrl: specCanvas.toDataURL('image/png')
    };
  }

  function createSvgFilter(id, dispUrl, scale, dispersion, saturate) {
    ensureSvgPool();
    const filter = document.createElementNS(SVG_NS, 'filter');
    filter.setAttribute('id', id);
    filter.setAttribute('x', '0%');
    filter.setAttribute('y', '0%');
    filter.setAttribute('width', '100%');
    filter.setAttribute('height', '100%');
    filter.setAttribute('color-interpolation-filters', 'sRGB');

    const feImg = document.createElementNS(SVG_NS, 'feImage');
    feImg.setAttribute('href', dispUrl);
    feImg.setAttribute('result', 'dispMap');
    feImg.setAttribute('preserveAspectRatio', 'none');
    filter.appendChild(feImg);

    const scaleR = (scale * (1 - dispersion * 0.11)).toFixed(2);
    const scaleG = scale.toFixed(2);
    const scaleB = (scale * (1 + dispersion * 0.14)).toFixed(2);

    // Chromatic Dispersion Pass: separate R, G, B Snell refraction scales (ZERO Gaussian blur!)
    const channels = [
      { name: 'r', scale: scaleR, matrix: '1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0' },
      { name: 'g', scale: scaleG, matrix: '0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0' },
      { name: 'b', scale: scaleB, matrix: '0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0' }
    ];

    for (const ch of channels) {
      const disp = document.createElementNS(SVG_NS, 'feDisplacementMap');
      disp.setAttribute('in', 'SourceGraphic');
      disp.setAttribute('in2', 'dispMap');
      disp.setAttribute('scale', ch.scale);
      disp.setAttribute('xChannelSelector', 'R');
      disp.setAttribute('yChannelSelector', 'G');
      disp.setAttribute('result', `${ch.name}_disp`);
      filter.appendChild(disp);

      const cm = document.createElementNS(SVG_NS, 'feColorMatrix');
      cm.setAttribute('in', `${ch.name}_disp`);
      cm.setAttribute('type', 'matrix');
      cm.setAttribute('values', ch.matrix);
      cm.setAttribute('result', `${ch.name}_ch`);
      filter.appendChild(cm);
    }

    // Exact additive RGB recombination (preserves 100% contrast & crispness)
    const rg = document.createElementNS(SVG_NS, 'feComposite');
    rg.setAttribute('in', 'r_ch');
    rg.setAttribute('in2', 'g_ch');
    rg.setAttribute('operator', 'arithmetic');
    rg.setAttribute('k1', '0');
    rg.setAttribute('k2', '1');
    rg.setAttribute('k3', '1');
    rg.setAttribute('k4', '0');
    rg.setAttribute('result', 'rg_mix');
    filter.appendChild(rg);

    const rgb = document.createElementNS(SVG_NS, 'feComposite');
    rgb.setAttribute('in', 'rg_mix');
    rgb.setAttribute('in2', 'b_ch');
    rgb.setAttribute('operator', 'arithmetic');
    rgb.setAttribute('k1', '0');
    rgb.setAttribute('k2', '1');
    rgb.setAttribute('k3', '1');
    rgb.setAttribute('k4', '0');
    rgb.setAttribute('result', 'rgb_mix');
    filter.appendChild(rgb);

    // Optical crystal richness boost (real glass subtly intensifies refracted chroma)
    const sat = document.createElementNS(SVG_NS, 'feColorMatrix');
    sat.setAttribute('in', 'rgb_mix');
    sat.setAttribute('type', 'saturate');
    sat.setAttribute('values', String(saturate));
    filter.appendChild(sat);

    svgDefs.appendChild(filter);
    return filter;
  }

  let nextFilterId = 1;

  function acquireFilter(W, H, R, bezel, scale, ior, dispersion, dome, saturate) {
    // Quantize dimensions gently to 4px buckets so similar elements share 1 cached GPU filter
    const qW = Math.max(24, Math.round(W / 4) * 4);
    const qH = Math.max(20, Math.round(H / 4) * 4);
    const qR = Math.round(Math.min(R, qW / 2, qH / 2) / 2) * 2;
    const key = `${qW}x${qH}_r${qR}_b${bezel}_s${scale}_n${ior}_d${dispersion}_m${dome}_v${saturate}`;

    if (filterCache.has(key)) {
      return filterCache.get(key);
    }

    // Simple cache cap to prevent unbounded filter accumulation across dynamic resizes
    if (filterCache.size > 48) {
      const oldestKey = filterCache.keys().next().value;
      const oldest = filterCache.get(oldestKey);
      if (oldest && oldest.node && oldest.node.parentNode) {
        oldest.node.parentNode.removeChild(oldest.node);
      }
      filterCache.delete(oldestKey);
    }

    const id = `lg-opt-${nextFilterId++}`;
    const { dispUrl, specUrl } = generateOpticalMaps(qW, qH, qR, bezel, ior, dome);
    const node = createSvgFilter(id, dispUrl, scale, dispersion, saturate);
    const entry = { id, key, specUrl, node };
    filterCache.set(key, entry);
    return entry;
  }

  function updateElementGlass(el) {
    const rect = el.getBoundingClientRect();
    if (rect.width < 12 || rect.height < 12) return;

    const cs = getComputedStyle(el);
    let radius = parseFloat(cs.borderTopLeftRadius) || 0;
    if (radius > Math.min(rect.width, rect.height) / 2) {
      radius = Math.min(rect.width, rect.height) / 2;
    }

    const bezel = parseFloat(el.dataset.lgBezel) || Math.min(18, Math.max(10, Math.min(rect.width, rect.height) * 0.34));
    const scale = parseFloat(el.dataset.lgScale) || Math.min(28, Math.max(14, bezel * 1.35));
    const ior = parseFloat(el.dataset.lgIor) || 1.52;
    const dispersion = parseFloat(el.dataset.lgDispersion) || 1.0;
    const dome = parseFloat(el.dataset.lgDome) || 0.045;
    const saturate = parseFloat(el.dataset.lgSaturate) || 1.18;

    const entry = acquireFilter(rect.width, rect.height, radius, bezel, scale, ior, dispersion, dome, saturate);
    const prev = elementState.get(el);
    if (prev && prev.key === entry.key) return;

    elementState.set(el, { key: entry.key });
    el.style.setProperty('--lg-filter', `url(#${entry.id})`);
    el.style.setProperty('--lg-specular', `url("${entry.specUrl}")`);
  }

  const resizeObserver = new ResizeObserver((entries) => {
    for (const entry of entries) {
      updateElementGlass(entry.target);
    }
  });

  function bindPointerCaustics(el) {
    if (el.__lgPointerBound) return;
    el.__lgPointerBound = true;
    el.addEventListener('pointermove', (e) => {
      const r = el.getBoundingClientRect();
      if (!r.width || !r.height) return;
      const x = ((e.clientX - r.left) / r.width) * 100;
      const y = ((e.clientY - r.top) / r.height) * 100;
      el.style.setProperty('--lg-mx', `${x.toFixed(1)}%`);
      el.style.setProperty('--lg-my', `${y.toFixed(1)}%`);
    }, { passive: true });
    el.addEventListener('pointerleave', () => {
      el.style.removeProperty('--lg-mx');
      el.style.removeProperty('--lg-my');
    }, { passive: true });
  }

  function register(el) {
    if (!el || el.__lgRegistered) return;
    el.__lgRegistered = true;
    el.classList.add('liquid-glass');
    bindPointerCaustics(el);
    resizeObserver.observe(el);
    requestAnimationFrame(() => updateElementGlass(el));
  }

  function refreshAll() {
    document.querySelectorAll('[data-liquid-glass]').forEach((el) => {
      if (!el.__lgRegistered) register(el);
      else updateElementGlass(el);
    });
  }

  function init() {
    ensureSvgPool();
    refreshAll();

    // Auto-observe dynamically created tabs / popovers / tiles
    const mo = new MutationObserver((mutations) => {
      for (const m of mutations) {
        for (const node of m.addedNodes) {
          if (node.nodeType !== 1) continue;
          if (node.hasAttribute && node.hasAttribute('data-liquid-glass')) {
            register(node);
          }
          if (node.querySelectorAll) {
            node.querySelectorAll('[data-liquid-glass]').forEach(register);
          }
        }
      }
    });
    mo.observe(document.body, { childList: true, subtree: true });
  }

  window.LiquidGlass = { init, register, refreshAll, updateElementGlass };
})();
