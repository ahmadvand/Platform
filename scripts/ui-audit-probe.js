(async () => {
  const out = { dds: 0, badAnchor: [], sqBtns: [], narrow: [], overflowFld: [] };
  const sels = [...document.querySelectorAll('select')].filter(s => s.closest('.hr-dd') && !s.disabled).slice(0, 5);
  for (const s of sels) {
    const dd = s.closest('.hr-dd');
    try {
      dd._btn.click();
      out.dds++;
      await new Promise(r => setTimeout(r, 40));
      const b = dd._btn.getBoundingClientRect(), m = dd._menu.getBoundingClientRect();
      const dx = Math.round(m.right - b.right);
      const opensUp = m.bottom <= b.top + 2; // فاز ۹۵ — منوی رو به بالا طبیعی است
      const dy = opensUp ? Math.round(b.top - m.bottom) : Math.round(m.top - b.bottom - 4);
      if (Math.abs(dx) > 6 || Math.abs(dy) > 6) out.badAnchor.push((s.name || s.id || '?') + ' dx=' + dx + ' dy=' + dy + (opensUp ? '(up)' : ''));
      document.body.click();
      await new Promise(r => setTimeout(r, 25));
    } catch (e) { out.badAnchor.push('ERR ' + (s.name || s.id)); }
  }
  document.querySelectorAll('.btn').forEach(b => {
    if (b.offsetParent && b.offsetHeight > 44) out.sqBtns.push((b.textContent || '').trim().slice(0, 16) + '#h' + b.offsetHeight);
  });
  document.querySelectorAll('.fld input:not([type=checkbox]):not([type=radio]):not([type=hidden]), .fld select').forEach(i => {
    const r = i.getBoundingClientRect();
    if (r.width > 0 && r.width < 80 && !i.classList.contains('hr-dd-src')) out.narrow.push((i.name || i.id || '?') + ' w' + Math.round(r.width));
  });
  document.querySelectorAll('.fld').forEach(f => {
    const fr = f.getBoundingClientRect();
    if (!fr.width) return;
    const kid = f.querySelector('.hr-dd') || f.querySelector('input, select');
    if (!kid) return;
    const kr = kid.getBoundingClientRect();
    if (kr.width && (kr.right > fr.right + 1.5 || kr.left < fr.left - 1.5)) {
      out.overflowFld.push(((kid.id || kid.name || kid.className || '?') + '').toString().slice(0, 18) + ' ovl' + Math.round(kr.right - fr.right));
    }
  });
  return JSON.stringify(out);
})()
