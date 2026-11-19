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
    await admin.goto(B + '/admin/books/items/new'); await admin.fill('#ClassName', cls); await admin.fill('#Name', name);
    await admin.fill('#PurchasePrice', String(pp)); await admin.fill('#SalePrice', String(sp)); await admin.click('button:has-text("Add book")'); await admin.waitForLoadState();
    itemUrl[name] = admin.url().split('?')[0];
    await admin.selectOption('#kind', 'purchase'); await admin.fill('#quantity', String(st)); await admin.fill('#note', 'DEMO opening stock'); await admin.click('button:has-text("Save stock")'); await admin.waitForLoadState();
  }
  const stockOf = async name => { await admin.goto(itemUrl[name]); return Number((await admin.locator('#stock-h strong').innerText()).trim()); };
  ok('stock entered', (await stockOf('DEMO English Reader 1')) === 40);
  await admin.selectOption('#kind', 'correction'); await admin.fill('#quantity', '-999'); await admin.fill('#note', 'DEMO'); await admin.click('button:has-text("Save stock")'); await admin.waitForLoadState();
  ok('stock never below zero', (await admin.content()).includes('Stock cannot go below zero'));
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

  await admin.goto(B + '/admin/books/orders'); await admin.screenshot({ path: OUT + '/admin-orders.png', fullPage: true });
  ok('no script errors ' + errors.join(' | '), errors.length === 0);
  console.log(results.join('\n'));
  console.log((results.filter(r => r.startsWith('FAIL')).length === 0 ? 'ALL PASSED' : 'SOME FAILED') + ' (' + results.length + ' checks)');
  await browser.close();
  process.exit(results.some(r => r.startsWith('FAIL')) ? 1 : 0);
})().catch(e => { console.log(results.join('\n')); console.error(e); process.exit(1); });
