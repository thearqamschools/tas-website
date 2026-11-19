// End-to-end tests of the books money and stock rules. See README.md. Synthetic data only.
const { chromium, devices } = require('playwright');
const crypto = require('crypto');
const [B, EMAIL, PASSWORD, SECRET, OUT, JPEG] = process.argv.slice(2);
if (!B || !SECRET || !OUT || !JPEG) { console.error('usage: node books-e2e.js <base> <email> <password> <totp-secret> <out-dir> <test.jpg>'); process.exit(2); }

function b32(s) { const a = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'; let bits = ''; for (const c of s.replace(/\s/g, '')) bits += a.indexOf(c).toString(2).padStart(5, '0'); const out = []; for (let i = 0; i + 8 <= bits.length; i += 8) out.push(parseInt(bits.slice(i, i + 8), 2)); return Buffer.from(out); }
function totp(secret) { const step = Math.floor(Date.now() / 30000); const buf = Buffer.alloc(8); buf.writeBigUInt64BE(BigInt(step)); const h = crypto.createHmac('sha1', b32(secret)).update(buf).digest(); const o = h[h.length - 1] & 15; return String((((h[o] & 127) << 24) | (h[o + 1] << 16) | (h[o + 2] << 8) | h[o + 3]) % 1e6).padStart(6, '0'); }
const results = []; const ok = (name, cond) => results.push((cond ? 'PASS ' : 'FAIL ') + name);
const money = s => Number(String(s).replace('Rs.', '').replace(/[^0-9.]/g, ''));

const BOOKS = [
  ['Play Group', 'DEMO Colours and Shapes', 150, 220, 30], ['Play Group', 'DEMO Rhymes Book', 120, 180, 20],
  ['Nursery', 'DEMO Alphabet Book', 180, 260, 25], ['Class 1', 'DEMO English Reader 1', 250, 350, 40],
  ['Class 1', 'DEMO Maths 1', 230, 320, 3], ['Class 2', 'DEMO Urdu Reader 2', 240, 340, 15]];

(async () => {
  const browser = await chromium.launch();
  const admin = await (await browser.newContext({ viewport: { width: 1366, height: 900 } })).newPage();
  const errors = []; admin.on('pageerror', e => errors.push(e.message)); admin.on('dialog', d => d.accept());
  await admin.goto(B + '/admin/login'); await admin.fill('#Email', EMAIL); await admin.fill('#Password', PASSWORD); await admin.click('text=Continue'); await admin.waitForLoadState();
  await admin.fill('#Code', totp(SECRET)); await admin.click('button[type=submit]'); await admin.waitForLoadState();

  // --- Books, stock and customers -----------------------------------------------------------
  const itemUrl = {};
  for (const [cls, name, pp, sp, st] of BOOKS) {
    await admin.goto(B + '/admin/books/items/new'); await admin.selectOption('#ClassName', cls); await admin.fill('#Name', name);
    await admin.fill('#PurchasePrice', String(pp)); await admin.fill('#SalePrice', String(sp)); await admin.click('button:has-text("Add book")'); await admin.waitForLoadState();
    itemUrl[name] = admin.url().split('?')[0];
    await admin.selectOption('#kind', 'purchase'); await admin.fill('#quantity', String(st)); await admin.fill('#note', 'DEMO opening stock'); await admin.click('button:has-text("Save stock")'); await admin.waitForLoadState();
  }
  const stockOf = async name => { await admin.goto(itemUrl[name]); return Number((await admin.locator('#stock-h strong').innerText()).trim()); };
  ok('stock entered', (await stockOf('DEMO English Reader 1')) === 40);
  await admin.selectOption('#kind', 'correction'); await admin.fill('#quantity', '-999'); await admin.fill('#note', 'DEMO'); await admin.click('button:has-text("Save stock")'); await admin.waitForLoadState();
  ok('stock never below zero', (await admin.content()).includes('Stock cannot go below zero'));
  // Retail price less discount gives the sale price; vendor and retail are office-only.
  await admin.goto(B + '/admin/books/items/new'); await admin.selectOption('#ClassName', 'Class 3'); await admin.fill('#Name', 'DEMO Retail Book');
  await admin.fill('#Vendor', 'DEMO Vendor Ltd'); await admin.fill('#PurchasePrice', '400'); await admin.fill('#RetailPrice', '600'); await admin.fill('#DiscountPercent', '25%');
  ok('sale price worked out as you type (450)', (await admin.inputValue('#SalePrice')) === '450');
  await admin.click('button:has-text("Add book")'); await admin.waitForLoadState();
  itemUrl['DEMO Retail Book'] = admin.url().split('?')[0];
  await admin.selectOption('#Category', 'stationery'); await admin.click('button:has-text("Save changes")'); await admin.waitForLoadState();
  await admin.fill('#Vendor', 'DEMO Vendor Ltd'); await admin.click('button:has-text("Save changes")'); await admin.waitForLoadState();
  ok('category kept after saving again', (await admin.inputValue('#Category')) === 'stationery');
  await admin.selectOption('#Category', 'books'); await admin.click('button:has-text("Save changes")'); await admin.waitForLoadState();
  ok('sale price saved as retail less discount', (await admin.inputValue('#SalePrice')) === '450' && (await admin.inputValue('#Vendor')) === 'DEMO Vendor Ltd');
  await admin.fill('#quantity', '4'); await admin.fill('#note', 'DEMO'); await admin.click('button:has-text("Save stock")'); await admin.waitForLoadState();
  await admin.fill('#level', '5'); await admin.click('button:has-text("Save level")'); await admin.waitForLoadState();
  ok('minimum level saved and warning shown', (await admin.content()).includes('Minimum stock level saved') && (await admin.content()).includes('Low stock: 4 in hand, minimum level 5'));
  await admin.screenshot({ path: OUT + '/admin-item-form.png', fullPage: true });
  await admin.goto(B + '/admin/books');
  const lowText = await admin.locator('.admin-card', { hasText: 'Low stock' }).innerText();
  ok('overview lists the low book by its own level', lowText.includes('DEMO Retail Book') && lowText.includes('minimum 5') && !lowText.includes('DEMO English Reader 1'));
  await admin.goto(B + '/admin/books/items/new'); await admin.fill('#Name', 'DEMO Bad Class'); await admin.evaluate(() => { const s = document.getElementById('ClassName'); const o = document.createElement('option'); o.value = 'Class 99'; s.appendChild(o); s.value = 'Class 99'; });
  await admin.fill('#SalePrice', '10'); await admin.click('button:has-text("Add book")'); await admin.waitForLoadState();
  ok('class must come from the list', (await admin.content()).includes('Please choose the class from the list'));
  for (const [mobile, name, campus, address] of [['0300 1234567', 'DEMO Customer (test)', 'DEMO Campus', 'House 1, Street 2, Example Town, Faisalabad'], ['0311-7654321', 'DEMO Pickup Only', '', '']]) {
    await admin.goto(B + '/admin/books/customers/new'); await admin.fill('#Mobile', mobile); await admin.fill('#Name', name);
    if (campus) await admin.fill('#CampusName', campus); if (address) await admin.fill('#Address', address);
    await admin.click('button:has-text("Add customer")'); await admin.waitForLoadState();
  }
  ok('customer mobile stored in one format', (await admin.inputValue('#Mobile')) === '0311-7654321');
  for (const [n, kind] of [['DEMO Office cash', 'cash'], ['DEMO Bank account', 'bank']]) {
    await admin.goto(B + '/admin/books/accounts'); const f = admin.locator('form', { hasText: 'Add an account' });
    await f.locator('input[name=Name]').fill(n); await f.locator('select[name=Kind]').selectOption(kind); await f.locator('button:has-text("Add account")').click(); await admin.waitForLoadState();
  }
  ok('accounts added', (await admin.locator('form.admin-account').count()) === 3);

  // --- Customer order (phone size) ------------------------------------------------------------
  const cust = await (await browser.newContext({ ...devices['iPhone 13'] })).newPage(); cust.on('pageerror', e => errors.push(e.message));
  await cust.goto(B + '/books'); await cust.fill('#bk-mobile', '0300-9999999'); await cust.click('button:has-text("Continue")'); await cust.waitForLoadState();
  ok('unregistered number refused', (await cust.content()).includes('not registered'));
  await cust.fill('#bk-mobile', '0300-1234567'); await cust.click('button:has-text("Continue")'); await cust.waitForLoadState();
  const q = async (name, v) => cust.fill('#' + await cust.locator('.books-row', { hasText: name }).locator('input').getAttribute('id'), String(v));
  await q('DEMO English Reader 1', 10); await q('DEMO Alphabet Book', 5); await cust.waitForTimeout(150);
  ok('live total 10×350 + 5×260 = 4,800', money(await cust.locator('[data-books-total]').innerText()) === 4800);
  await q('DEMO Maths 1', 5); await cust.click('button:has-text("Preview order")'); await cust.waitForLoadState(); await cust.click('button:has-text("Place order")'); await cust.waitForLoadState();
  ok('cannot order more than stock', (await cust.content()).includes('only 3 in stock'));
  await q('DEMO Maths 1', 3); await cust.check('input[name=fulfilment][value=delivery]'); await cust.click('button:has-text("Preview order")'); await cust.waitForLoadState();
  await cust.screenshot({ path: OUT + '/m-preview.png', fullPage: true });
  await cust.click('button:has-text("Place order")'); await cust.waitForLoadState();
  const orderNo = Number((cust.url().match(/orders\/(\d+)/) || [])[1]);
  ok('order placed ' + orderNo, orderNo > 0 && (await cust.content()).includes('has been placed'));
  ok('placing an order takes no stock', (await stockOf('DEMO Maths 1')) === 3);
  await cust.goto(B + '/books/orders/' + orderNo + '/edit'); await q('DEMO English Reader 1', 12); await q('DEMO Alphabet Book', 0);
  await cust.click('button:has-text("Preview order")'); await cust.waitForLoadState(); await cust.click('button:has-text("Save changes")'); await cust.waitForLoadState();
  ok('customer edit saved and recorded', (await cust.content()).includes('changed 1 time'));
  ok('full delivery address shown to the customer', (await cust.content()).includes('House 1, Street 2, Example Town, Faisalabad'));
  // Customer sees only the sale price: no vendor, retail price or discount.
  await cust.goto(B + '/books/order');
  const pub = await cust.content();
  ok('customer sees sale price only', pub.includes('Rs. 450') && !pub.includes('DEMO Vendor') && !pub.includes('Rs. 600') && !pub.includes('25%'));
  ok('order form has Price / Quantity / Total headings', (await cust.locator('.books-row--head').first().innerText()).replace(/\s+/g, ' ').toUpperCase().includes('BOOK PRICE QUANTITY TOTAL'));
  await cust.screenshot({ path: OUT + '/m-order-form.png', fullPage: true });
  // Customer cancels a New order; after the office checks an order, the customer can no longer change or cancel it.
  await q('DEMO Rhymes Book', 1); await cust.click('button:has-text("Preview order")'); await cust.waitForLoadState(); await cust.click('button:has-text("Place order")'); await cust.waitForLoadState();
  const cancelNo = Number((cust.url().match(/orders\/(\d+)/) || [])[1]);
  cust.once('dialog', d => d.accept());
  await cust.click('button:has-text("Cancel this order")'); await cust.waitForLoadState();
  ok('customer cancelled a new order', (await cust.content()).includes('has been cancelled') && (await cust.locator('.books-detail-status').innerText()).includes('Cancelled'));
  await cust.goto(B + '/books/order'); await q('DEMO Rhymes Book', 1); await cust.click('button:has-text("Preview order")'); await cust.waitForLoadState(); await cust.click('button:has-text("Place order")'); await cust.waitForLoadState();
  const checkedNo = Number((cust.url().match(/orders\/(\d+)/) || [])[1]);
  await admin.goto(B + '/admin/books/orders/' + checkedNo); await admin.click('button:has-text("Mark as checked")'); await admin.waitForLoadState();
  await cust.goto(B + '/books/orders/' + checkedNo);
  ok('no change or cancel after the office checks', (await cust.locator('button:has-text("Cancel this order")').count()) === 0 && !(await cust.content()).includes('Change this order'));
  await cust.goto(B + '/books/orders/' + checkedNo + '/edit'); ok('edit page closed after checked', cust.url().endsWith('/books/orders/' + checkedNo));
  const forced = await cust.evaluate(async (no) => {
    const r = await fetch('/books/orders/' + no); const html = await r.text();
    const t = (html.match(/name="__RequestVerificationToken" type="hidden" value="([^"]+)"/) || [])[1] || document.querySelector('input[name=__RequestVerificationToken]').value;
    const p = await fetch('/books/orders/' + no + '/cancel', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: '__RequestVerificationToken=' + encodeURIComponent(t) });
    return p.text();
  }, checkedNo);
  ok('cancel refused by the server after checked', forced.includes('can no longer be cancelled'));
  await admin.goto(B + '/admin/books/orders/' + checkedNo); await admin.click('button:has-text("Cancel order")'); await admin.waitForLoadState();
  // total now 12×350 + 3×320 = 5,160

  // --- Office: order page, payments, stock -----------------------------------------------------
  const orderUrl = B + '/admin/books/orders/' + orderNo;
  await admin.goto(B + '/admin'); ok('dashboard shows new book orders', (await admin.locator('.admin-stat', { hasText: 'New book orders' }).locator('.admin-stat-value').innerText()).trim() === '1');
  await admin.goto(B + '/admin/books/find?code=BK-' + orderNo); ok('barcode search opens the order', admin.url().startsWith(orderUrl));
  ok('edit shown to the office', (await admin.content()).includes('Changes to this order') && (await admin.content()).includes('by the customer'));
  const balance = async () => money(await admin.locator('.admin-money .is-total td').innerText());
  ok('balance 5,160', (await balance()) === 5160);
  const pay = async (amount, discount, method, account, file) => {
    await admin.goto(orderUrl);
    if (amount !== null) await admin.fill('#Amount', String(amount)); if (discount) await admin.fill('#Discount', String(discount));
    await admin.selectOption('#Method', method);
    if (account) await admin.evaluate(name => { const s = document.getElementById('AccountId'); const o = [...s.options].find(x => x.text.includes(name)); s.value = o ? o.value : ''; if (o) { o.disabled = false; } }, account);
    if (file) await admin.setInputFiles('#Receipt', file);
    await admin.click('button:has-text("Save payment")'); await admin.waitForLoadState();
  };
  await pay(1000, 0, 'bank', 'DEMO Office cash');
  ok('method must match account', (await admin.content()).includes('is a cash account'));
  await pay(6000, 0, 'cash', 'DEMO Office cash');
  ok('cannot pay more than the balance', (await admin.content()).includes('more than the balance'));
  await pay(2000, 160, 'cash', 'DEMO Office cash');
  ok('part payment recorded', (await admin.content()).includes('Payment recorded') && (await balance()) === 3000 && (await admin.content()).includes('Part paid'));
  ok('stock taken at first payment (40-12=28)', (await stockOf('DEMO English Reader 1')) === 28);
  ok('stock taken at first payment (3-3=0)', (await stockOf('DEMO Maths 1')) === 0);
  await cust.goto(B + '/books/orders/' + orderNo);
  ok('customer cannot edit after payment', !(await cust.content()).includes('Change this order'));
  ok('customer never sees the discount', !(await cust.content()).toLowerCase().includes('discount'));
  await cust.goto(B + '/books/orders/' + orderNo + '/edit'); ok('edit page closed after payment', cust.url().endsWith('/books/orders/' + orderNo));
  await pay(3000, 0, 'bank', 'DEMO Bank account', JPEG);
  ok('second payment, fully paid', (await balance()) === 0 && (await admin.locator('.admin-order-head').innerText()).includes('Paid'));
  ok('stock not taken again', (await stockOf('DEMO English Reader 1')) === 28);
  await admin.goto(orderUrl);
  ok('no payment form when paid', (await admin.locator('#Amount').count()) === 0);
  ok('received by = signed-in user', (await admin.locator('#payments table').innerText()).includes('Preview Admin'));
  const receipt = await admin.locator('a:has-text("View")').first().getAttribute('href');
  const r1 = await admin.context().request.get(B + receipt); ok('receipt visible to admin', r1.status() === 200 && r1.headers()['content-type'].startsWith('image/jpeg'));
  const r2 = await cust.context().request.get(B + receipt, { maxRedirects: 0 }); ok('receipt not visible to others', r2.status() !== 200);
  ok('cancel not offered after payment', (await admin.locator('button:has-text("Cancel order")').count()) === 0);
  await admin.click('button:has-text("Mark as checked")'); await admin.waitForLoadState();
  await admin.click('button:has-text("Mark as packed")'); await admin.waitForLoadState();
  await admin.click('button:has-text("Mark as delivered")'); await admin.waitForLoadState();
  ok('status delivered', (await admin.locator('.admin-order-head').innerText()).includes('Delivered'));
  await admin.screenshot({ path: OUT + '/admin-order.png', fullPage: true });

  // --- Invoice ----------------------------------------------------------------------------------
  await admin.goto(orderUrl + '/invoice');
  ok('invoice shows barcode and contact', (await admin.locator('svg.barcode').count()) === 1 && (await admin.content()).includes('WhatsApp: 0339-2406479'));
  await admin.screenshot({ path: OUT + '/invoice.png', fullPage: true });
  const pdf = await admin.context().request.get(orderUrl + '/invoice.pdf');
  const body = await pdf.body();
  ok('invoice PDF', pdf.status() === 200 && body.slice(0, 5).toString() === '%PDF-');
  require('fs').writeFileSync(OUT + '/invoice-' + orderNo + '.pdf', body);

  // --- Office order, short stock at payment ----------------------------------------------------
  await admin.goto(B + '/admin/books/orders/new?q=0311'); await admin.click('a:has-text("DEMO Pickup Only")'); await admin.waitForLoadState();
  await admin.fill('#' + await admin.locator('tr', { hasText: 'DEMO Rhymes Book' }).locator('input').getAttribute('id'), '5');
  await admin.click('button:has-text("Place order")'); await admin.waitForLoadState();
  const officeNo = Number((admin.url().match(/orders\/(\d+)/) || [])[1]);
  ok('office order placed ' + officeNo, officeNo > orderNo);
  await admin.goto(itemUrl['DEMO Rhymes Book']); await admin.selectOption('#kind', 'correction'); await admin.fill('#quantity', '-18'); await admin.fill('#note', 'DEMO count'); await admin.click('button:has-text("Save stock")'); await admin.waitForLoadState();
  await admin.goto(B + '/admin/books/orders/' + officeNo);
  await admin.fill('#Amount', '100'); await admin.selectOption('#Method', 'cash');
  await admin.evaluate(() => { const s = document.getElementById('AccountId'); const o = [...s.options].find(x => x.text.includes('DEMO Office cash')); s.value = o.value; });
  await admin.click('button:has-text("Save payment")'); await admin.waitForLoadState();
  ok('payment refused when stock is short', (await admin.content()).includes('not enough stock'));
  ok('nothing taken when refused', (await stockOf('DEMO Rhymes Book')) === 2);
  await admin.goto(B + '/admin/books/orders/' + officeNo);
  await admin.click('button:has-text("Mark as delivered")').catch(() => {});
  ok('pick-up order cannot be marked delivered', (await admin.locator('button:has-text("Mark as delivered")').count()) === 0);
  await admin.click('button:has-text("Cancel order")'); await admin.waitForLoadState();
  ok('unpaid order can be cancelled', (await admin.locator('.admin-order-head').innerText()).includes('Cancelled'));

  // --- Books handed over before any payment: stock comes off then, and not again at payment --------
  await admin.goto(B + '/admin/books/orders/new?q=0311'); await admin.click('a:has-text("DEMO Pickup Only")'); await admin.waitForLoadState();
  await admin.fill('#' + await admin.locator('tr', { hasText: 'DEMO Colours and Shapes' }).locator('input').getAttribute('id'), '2');
  await admin.click('button:has-text("Place order")'); await admin.waitForLoadState();
  const thirdNo = Number((admin.url().match(/orders\/(\d+)/) || [])[1]);
  await admin.click('button:has-text("Mark as collected")'); await admin.waitForLoadState();
  ok('collected before payment takes stock (30-2=28)', (await stockOf('DEMO Colours and Shapes')) === 28);
  await admin.goto(B + '/admin/books/orders/' + thirdNo);
  ok('no backwards status buttons', (await admin.locator('button:has-text("Mark as checked")').count()) === 0);
  const back = await admin.evaluate(async (no) => {
    const token = document.querySelector('input[name=__RequestVerificationToken]').value;
    const r = await fetch('/admin/books/orders/' + no + '/status', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'status=new&__RequestVerificationToken=' + encodeURIComponent(token) });
    return r.text();
  }, thirdNo);
  ok('status cannot go back', back.includes('cannot go back'));
  await admin.fill('#Amount', '440'); await admin.selectOption('#Method', 'cash');
  await admin.evaluate(() => { const s = document.getElementById('AccountId'); const o = [...s.options].find(x => x.text.includes('DEMO Office cash')); s.value = o.value; });
  await admin.click('button:has-text("Save payment")'); await admin.waitForLoadState();
  ok('payment after hand-over does not take stock again', (await stockOf('DEMO Colours and Shapes')) === 28);


  // --- Returns (part 3): books back into stock, money paid back at once ----------------------------
  const pickCash = (sel) => admin.evaluate((id) => { const s = document.getElementById(id); const o = [...s.options].find(x => x.text.includes('DEMO Office cash')); s.value = o.value; }, sel);
  await admin.goto(B + '/admin/books/orders/' + thirdNo);
  const rInput = admin.locator('#returns tr', { hasText: 'DEMO Colours and Shapes' }).locator('input');
  await rInput.fill('3'); await admin.selectOption('#ReturnMethod', 'cash'); await pickCash('ReturnAccountId');
  await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  ok('cannot return more than taken', (await admin.content()).includes('at most 2 can come back'));
  ok('nothing changed when refused', (await stockOf('DEMO Colours and Shapes')) === 28);
  await admin.goto(B + '/admin/books/orders/' + thirdNo);
  await admin.locator('#returns tr', { hasText: 'DEMO Colours and Shapes' }).locator('input').fill('1');
  await admin.selectOption('#ReturnMethod', 'cash'); await pickCash('ReturnAccountId'); await admin.fill('#ReturnNote', 'DEMO damaged cover');
  await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  const rn = Number((admin.url().match(/rn=(\d+)/) || [])[1]);
  ok('return recorded ' + rn, rn >= 101 && (await admin.locator('.admin-notice, .notice, [role=status]').first().innerText().catch(() => '')).includes('RN-' + rn));
  ok('returned book back in stock (28+1=29)', (await stockOf('DEMO Colours and Shapes')) === 29);
  await admin.goto(B + '/admin/books/orders/' + thirdNo);
  const retRow = await admin.locator('#returns table').first().innerText();
  ok('refund = value when fully paid (Rs. 220)', retRow.includes('RN-' + rn) && /Rs\. 220/.test(retRow));
  await admin.screenshot({ path: OUT + '/admin-order-returns.png', fullPage: true });
  await admin.goto(B + '/admin/books/returns/' + rn);
  ok('return note page', (await admin.content()).includes('RETURN NOTE') && (await admin.content()).includes('WhatsApp: 0339-2406479'));
  await admin.screenshot({ path: OUT + '/return-note.png', fullPage: true });

  // Discount and part payment: a discount is never paid out as cash; two returns in a row end at balance 0.
  await admin.goto(B + '/admin/books/orders/new?q=0311'); await admin.click('a:has-text("DEMO Pickup Only")'); await admin.waitForLoadState();
  await admin.fill('#' + await admin.locator('tr', { hasText: 'DEMO Alphabet Book' }).locator('input').getAttribute('id'), '4');
  await admin.click('button:has-text("Place order")'); await admin.waitForLoadState();
  const fourthNo = Number((admin.url().match(/orders\/(\d+)/) || [])[1]);   // 4 × 260 = 1,040
  await admin.fill('#Amount', '500'); await admin.fill('#Discount', '40'); await admin.selectOption('#Method', 'cash'); await pickCash('AccountId');
  await admin.click('button:has-text("Save payment")'); await admin.waitForLoadState();
  const balanceOf = async () => money(await admin.locator('tr', { hasText: 'Balance' }).last().locator('td').innerText());
  ok('discount order balance 500', (await balanceOf()) === 500);
  const tooEarly = new Date(Date.now() + 5 * 3600e3 + 3 * 86400e3).toISOString().slice(0, 10);
  await admin.locator('#returns tr', { hasText: 'DEMO Alphabet Book' }).locator('input').fill('2'); await admin.fill('#ReturnedOn', tooEarly);
  await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  ok('return date in the future refused', (await admin.content()).includes('cannot be in the future'));
  await admin.goto(B + '/admin/books/orders/' + fourthNo);
  await admin.locator('#returns tr', { hasText: 'DEMO Alphabet Book' }).locator('input').fill('2');
  await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  ok('first return: value after discount 500, nothing paid back, balance 0', (await balanceOf()) === 0 && /Rs\. 500\s+–/.test(await admin.locator('#returns table').first().innerText()));
  await admin.locator('#returns tr', { hasText: 'DEMO Alphabet Book' }).locator('input').fill('2'); await admin.selectOption('#ReturnMethod', 'cash'); await pickCash('ReturnAccountId');
  await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  const retText = await admin.locator('#returns table').first().innerText();
  const backs = [...retText.matchAll(/RN-\d+[^\n]*/g)].map(m => m[0]);
  ok('second return pays back 500 = cash received (not 520)', (await balanceOf()) === 0 && backs.length === 2 && /Rs\. 500\s+Rs\. 500 \(cash\)/.test(backs[1]));
  ok('all four books back in stock (25)', (await stockOf('DEMO Alphabet Book')) === 25);

  // --- Reports ----------------------------------------------------------------------------------
  const today = new Date(Date.now() + 5 * 3600e3).toISOString().slice(0, 10);
  for (const kind of ['stock', 'sales-books', 'sales-customers', 'payments', 'unpaid', 'discounts', 'returns', 'profit']) {
    const q = B + '/admin/books/reports?kind=' + kind + '&from=' + today.slice(0, 8) + '01&to=' + today;
    await admin.goto(q);
    ok('report ' + kind + ' shows', (await admin.locator('.admin-report table').count()) === 1);
    if (kind === 'stock' || kind === 'returns' || kind === 'profit') await admin.screenshot({ path: OUT + '/report-' + kind + '.png', fullPage: true });
    const p = await admin.context().request.get(q + '&format=pdf'); const pb = await p.body();
    const x = await admin.context().request.get(q + '&format=xlsx'); const xb = await x.body();
    ok('report ' + kind + ' PDF and Excel', pb.slice(0, 5).toString() === '%PDF-' && xb.slice(0, 2).toString() === 'PK' && (x.headers()['content-type'] || '').includes('spreadsheetml'));
    require('fs').writeFileSync(OUT + '/report-' + kind + '.pdf', pb); require('fs').writeFileSync(OUT + '/report-' + kind + '.xlsx', xb);
  }
  await admin.goto(B + '/admin/books/reports?kind=returns&from=' + today + '&to=' + today);
  ok('returns report lists RN-' + rn, (await admin.locator('.admin-report').innerText()).includes('RN-' + rn));
  await admin.goto(B + '/admin/books/reports?kind=stock');
  ok('stock report shows Colours 29', /DEMO Colours and Shapes[^\n]*29/.test(await admin.locator('.admin-report').innerText()));
  const anon = await cust.context().request.get(B + '/admin/books/reports?kind=stock&format=xlsx', { maxRedirects: 0 });
  ok('reports not visible to others', anon.status() !== 200);

  await admin.goto(B + '/admin/books/orders'); await admin.screenshot({ path: OUT + '/admin-orders.png', fullPage: true });
  ok('no script errors ' + errors.join(' | '), errors.length === 0);
  console.log(results.join('\n'));
  console.log((results.filter(r => r.startsWith('FAIL')).length === 0 ? 'ALL PASSED' : 'SOME FAILED') + ' (' + results.length + ' checks)');
  await browser.close();
  process.exit(results.some(r => r.startsWith('FAIL')) ? 1 : 0);
})().catch(e => { console.log(results.join('\n')); console.error(e); process.exit(1); });
