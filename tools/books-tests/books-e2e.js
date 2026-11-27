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
  // Order actions: the next step is a button at the top; the others are under More (30 November 2026).
  const adminAct = async text => { const btn = admin.locator('button:has-text("' + text + '")').first();
    if (!(await btn.isVisible())) { const more = admin.locator('.admin-order-actions details.admin-more > summary'); if (await more.count()) await more.first().click(); }
    await btn.click(); };

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
  ok('accounts added', (await admin.locator('form.admin-account').count()) >= 3);

  // --- Customer order (phone size) ------------------------------------------------------------
  const cust = await (await browser.newContext({ ...devices['iPhone 13'] })).newPage(); cust.on('pageerror', e => errors.push(e.message));
  await cust.goto(B + '/books'); await cust.fill('#bk-mobile', '0300-9999999'); await cust.click('button:has-text("Continue")'); await cust.waitForLoadState();
  ok('unregistered number refused', (await cust.content()).includes('not registered'));
  ok('contact lines: Phone and Mobile/WhatsApp', (await cust.locator('.alert-error').innerText()).includes('Mobile/WhatsApp: 0339-2406479') && (await cust.locator('.alert-error').innerText()).includes('Phone: 041-2660265, 041-2406479'));
  await cust.fill('#bk-mobile', '0300-1234567'); await cust.click('button:has-text("Continue")'); await cust.waitForLoadState();
  ok('sign-in opens New order with the tab bar', cust.url().endsWith('/books/order') && (await cust.locator('.books-tabs .books-tab').allInnerTexts()).map(t => t.trim()).join('|') === 'New order|My orders|Payments|More');
  ok('no greeting heading and no admissions band', !(await cust.content()).includes('Assalamualaikum') && !(await cust.content()).includes('learning journey'));
  // Quantities are set through the page's own box (hidden behind Add / - + and class tabs).
  const q = async (name, v) => { const id = await cust.locator('.books-row', { hasText: name }).locator('input[data-price]').getAttribute('id');
    await cust.evaluate(([i, val]) => { const e = document.getElementById(i); e.value = val ? String(val) : ''; e.dispatchEvent(new Event('input', { bubbles: true })); }, [id, v]); };
  const go = async sel => { await Promise.all([cust.waitForNavigation(), cust.click(sel)]); };
  // New order: Continue -> (Pick up / Delivery) -> Continue to payment -> Place order (bank + receipt) or pay later.
  const placeOrder = async (books, opts) => { opts = opts || {}; await cust.goto(B + '/books/order'); for (const [n, v] of books) await q(n, v);
    await go('.books-sticky button:has-text("Continue")'); if (cust.url().includes('/books/orders/')) return;
    if ((await cust.locator('button:has-text("Continue to payment")').count()) === 0) return;
    if (opts.fulfilment) await cust.check('input[name=fulfilment][value=' + opts.fulfilment + ']');
    await go('button:has-text("Continue to payment")');
    if (opts.bank) { await cust.locator('.books-pay-opt', { hasText: opts.bank }).locator('input').check(); if (opts.file !== false) await cust.setInputFiles('#op-file', JPEG); await go('button:has-text("Place order"):not(:has-text("later"))'); }
    else if (opts.cash) { await cust.locator('.books-pay-opt', { hasText: 'Cash' }).locator('input').check(); await go('button:has-text("Place order"):not(:has-text("later"))'); }
    else await go('button:has-text("Place order, pay later")'); };
  const no = () => Number((cust.url().match(/orders\/(\d+)/) || [])[1]);
  const status = async () => (await cust.locator('.books-order-head .books-status').innerText());
  await q('DEMO English Reader 1', 10); await q('DEMO Alphabet Book', 5); await cust.waitForTimeout(150);
  ok('live total 10×350 + 5×260 = 4,800', money(await cust.locator('[data-books-total]').innerText()) === 4800);
  ok('all classes one after the other with quantity boxes, no class tabs or whole-set button', (await cust.locator('.books-class-group legend').count()) > 1 && (await cust.locator('.books-class-chip, .books-set, .books-qty-add').count()) === 0 && (await cust.locator('.books-row input[type=number]').first().isVisible()));
  await cust.screenshot({ path: OUT + '/m-order-form.png', fullPage: true });
  await q('DEMO Maths 1', 5); await go('.books-sticky button:has-text("Continue")');
  ok('Pick up / Delivery asked on the check screen', (await cust.locator('.books-review-get').count()) === 1 && (await cust.locator('button:has-text("Continue to payment")').count()) === 1 && (await cust.locator('button:has-text("Change order")').count()) === 1);
  await go('button:has-text("Continue to payment")');
  ok('payment screen: total, banks and cash, no amount box', (await cust.locator('.books-pay-total').innerText()).includes('6,400') && (await cust.locator('.books-pay-opt', { hasText: 'Cash' }).count()) === 1 && (await cust.locator('input[name=PayAmount]').count()) === 0);
  await cust.screenshot({ path: OUT + '/m-pay-step.png', fullPage: true });
  await go('button:has-text("Place order, pay later")');
  ok('cannot order more than stock', (await cust.content()).includes('only 3 in stock'));
  await q('DEMO Maths 1', 3); await go('.books-sticky button:has-text("Continue")'); await cust.check('input[name=fulfilment][value=delivery]');
  await cust.screenshot({ path: OUT + '/m-preview.png', fullPage: true });
  await go('button:has-text("Continue to payment")'); await go('button:has-text("Place order, pay later")');
  const orderNo = no();
  ok('order placed ' + orderNo, orderNo > 0 && (await cust.content()).includes('placed.'));
  ok('placing an order takes no stock', (await stockOf('DEMO Maths 1')) === 3);
  await cust.goto(B + '/books/orders/' + orderNo + '/edit'); await q('DEMO English Reader 1', 12); await q('DEMO Alphabet Book', 0);
  await go('.books-sticky button:has-text("Continue")'); await go('button:has-text("Save changes")');
  ok('customer edit saved', (await cust.content()).includes('Your changes to order'));
  ok('full delivery address shown to the customer', (await cust.content()).includes('House 1, Street 2, Example Town, Faisalabad'));
  // Customer sees only the sale price: no vendor, retail price or discount.
  await cust.goto(B + '/books/order');
  const pub = await cust.content();
  ok('customer sees sale price only', pub.includes('Rs. 450') && !pub.includes('DEMO Vendor') && !pub.includes('Rs. 600') && !pub.includes('25%'));
  // Customer cancels a New order; after the office checks an order, the customer can no longer change or cancel it.
  await placeOrder([['DEMO Rhymes Book', 1]]);
  const cancelNo = no();
  ok('Change and Cancel side by side, no All my orders', (await cust.locator('.books-actions--pair').count()) === 1 && (await cust.locator('a:has-text("All my orders")').count()) === 0);
  cust.once('dialog', d => d.accept());
  await go('button:has-text("Cancel order")');
  ok('customer cancelled a new order', (await cust.content()).includes('has been cancelled') && (await status()).includes('Cancelled'));
  await placeOrder([['DEMO Rhymes Book', 1]]);
  const checkedNo = no();
  await admin.goto(B + '/admin/books/orders/' + checkedNo); await adminAct('Confirm order'); await admin.waitForLoadState();
  await cust.goto(B + '/books/orders/' + checkedNo);
  ok('no change or cancel after the office checks', (await cust.locator('button:has-text("Cancel order")').count()) === 0 && (await cust.locator('a:has-text("Change order")').count()) === 0);
  await cust.goto(B + '/books/orders/' + checkedNo + '/edit'); ok('edit page closed after checked', cust.url().endsWith('/books/orders/' + checkedNo));
  const forced = await cust.evaluate(async (no) => {
    const r = await fetch('/books/orders/' + no); const html = await r.text();
    const t = (html.match(/name="__RequestVerificationToken" type="hidden" value="([^"]+)"/) || [])[1] || document.querySelector('input[name=__RequestVerificationToken]').value;
    const p = await fetch('/books/orders/' + no + '/cancel', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: '__RequestVerificationToken=' + encodeURIComponent(t) });
    return p.text();
  }, checkedNo);
  ok('cancel refused by the server after checked', forced.includes('can no longer be cancelled'));
  await admin.goto(B + '/admin/books/orders/' + checkedNo); await adminAct('Cancel order'); await admin.waitForLoadState();

  // --- Customer pays by bank transfer and attaches the receipt; the office confirms or rejects -----------------
  await admin.goto(B + '/admin/books/accounts'); { const f = admin.locator('form', { hasText: 'Add an account' });
    await f.locator('input[name=Name]').fill('DEMO Customer Bank'); await f.locator('select[name=Kind]').selectOption('bank');
    await f.locator('input[name=AccountTitle]').fill('DEMO TITLE'); await f.locator('input[name=AccountNumber]').fill('12345678901');
    await f.locator('input[name=Iban]').fill('PK00DEMO0000012345678901'); await f.locator('input[name=ShowToCustomers][type=checkbox]').check();
    await f.locator('button:has-text("Add account")').click(); await admin.waitForLoadState(); }
  ok('bank account for customers saved', (await admin.content()).includes('PK00DEMO0000012345678901'));
  await placeOrder([['DEMO Retail Book', 2]], { bank: 'DEMO Customer Bank', file: false });
  ok('receipt required for a bank payment', (await cust.content()).includes('attach the receipt') && !cust.url().includes('/books/orders/'));
  await placeOrder([['DEMO Retail Book', 2]], { bank: 'DEMO Customer Bank' });
  const payNo = no();   // 2 x 450 = 900, placed together with the payment
  ok('order placed with its payment', payNo > 0 && (await cust.content()).includes('We will check your payment') && (await status()).includes('Payment being checked'));
  ok('tracking line shown', (await cust.locator('.books-track li').count()) === 4 && (await cust.locator('.books-track li.is-done').count()) === 1);
  await cust.screenshot({ path: OUT + '/m-payment.png', fullPage: true });
  ok('no change or cancel after sending a payment', (await cust.locator('button:has-text("Cancel order")').count()) === 0 && (await cust.locator('a:has-text("Change order")').count()) === 0);
  await placeOrder([['DEMO Rhymes Book', 1]], { cash: true });
  const cashNo = no();
  ok('cash: order placed, pay at the office', (await cust.content()).includes('in cash at the office') && (await status()).includes('Payment pending'));
  cust.once('dialog', d => d.accept()); await go('button:has-text("Cancel order")');
  await admin.goto(B + '/admin/books/orders?payment=tocheck');
  ok('office list shows payment to check', (await admin.content()).includes('payment to check') && (await admin.content()).includes('>BK-' + payNo + '<'));
  await admin.goto(B + '/admin/books/orders/' + payNo);
  const sentRec = await admin.locator('#sent a:has-text("View receipt")').getAttribute('href');
  const rr = await admin.context().request.get(B + sentRec); ok('office sees the receipt', rr.status() === 200 && rr.headers()['content-type'].startsWith('image/jpeg'));
  const rc = await cust.context().request.get(B + sentRec, { maxRedirects: 0 }); ok('customer cannot open the office receipt link', rc.status() !== 200);
  await admin.fill('#Amount', '400'); await admin.selectOption('#Method', 'bank');
  await admin.evaluate(() => { const s = document.getElementById('AccountId'); const o = [...s.options].find(x => x.text.includes('DEMO Customer Bank')); s.value = o.value; });
  await admin.click('button:has-text("Save payment")'); await admin.waitForLoadState();
  ok('no manual payment while a customer payment waits', (await admin.content()).includes('so the same money is not counted twice'));
  await admin.goto(B + '/admin/books/orders/' + payNo); await adminAct('Cancel order'); await admin.waitForLoadState();
  ok('office cannot cancel while a customer payment waits', (await admin.content()).includes('Please confirm or reject it first'));
  ok('office cannot change books while a customer payment waits', (await admin.locator('a:has-text("Change books")').count()) === 0);
  await admin.goto(B + '/admin/books/orders/' + payNo);
  await admin.fill('#sent input[name=Reason]', 'DEMO amount not received'); await admin.click('#sent button:has-text("Not accepted")'); await admin.waitForLoadState();
  await cust.goto(B + '/books/orders/' + payNo);
  ok('customer sees the reason and can send again', (await cust.content()).includes('DEMO amount not received') && (await cust.locator('button:has-text("Send payment")').count()) === 1);
  await cust.locator('#payment .books-pay-opt', { hasText: 'DEMO Customer Bank' }).locator('input').check(); await cust.setInputFiles('#bp-file', JPEG);
  await go('button:has-text("Send payment")');
  ok('nothing more to send while the whole amount is being checked', (await cust.locator('button:has-text("Send payment")').count()) === 0 && (await cust.locator('#payment').innerText()).includes('being checked by the office'));
  const before = await stockOf('DEMO Retail Book');
  await admin.goto(B + '/admin/books/orders/' + payNo);
  ok('the amount on the receipt must be typed', (await admin.locator('#sent input[name=Amount]').getAttribute('required')) !== null && (await admin.locator('#sent input[name=Amount]').inputValue()) === '');
  await admin.goto(B + '/admin/books/orders/' + payNo); await admin.fill('#sent input[name=Amount]', '900'); await admin.click('#sent button:has-text("Confirm payment")'); await admin.waitForLoadState();
  ok('office confirmed: payment recorded and order confirmed', (await admin.content()).includes('Payment confirmed and recorded') && (await admin.locator('.admin-order-head').innerText()).includes('Confirmed') && (await admin.locator('#payments table').innerText()).includes('900'));
  await admin.screenshot({ path: OUT + '/admin-sent-payment.png', fullPage: true });
  ok('stock taken at the confirmed payment', (await stockOf('DEMO Retail Book')) === before - 2);
  await cust.goto(B + '/books/orders/' + payNo);
  ok('customer sees confirmed', (await status()).includes('Confirmed') && (await cust.locator('#payment').innerText()).includes('Confirmed'));
  await admin.goto(B + '/admin/books/orders');
  ok('orders list shows the campus with the customer', (await admin.locator('.admin-table tbody').first().innerText()).includes('DEMO Campus'));
  ok('quick filters with counts', (await admin.locator('.admin-quick > a').count()) === 7 && (await admin.locator('.admin-quick > a[aria-current=page]').innerText()).startsWith('All'));
  await admin.goto(B + '/admin/books/orders?view=done');
  ok('Done filter lists only delivered or collected orders', (await admin.locator('.admin-table tbody tr').count()) === 0 || (await admin.locator('.admin-table tbody .admin-status').allInnerTexts()).every(t => /Delivered|Collected/.test(t)));
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
  ok('customer cannot edit after payment', (await cust.locator('a:has-text("Change order")').count()) === 0);
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
  await adminAct('Confirm order'); await admin.waitForLoadState();
  await adminAct('Mark as packed'); await admin.waitForLoadState();
  await adminAct('Mark as delivered'); await admin.waitForLoadState();
  ok('status delivered', (await admin.locator('.admin-order-head').innerText()).includes('Delivered'));
  await admin.screenshot({ path: OUT + '/admin-order.png', fullPage: true });

  // --- Invoice ----------------------------------------------------------------------------------
  await admin.goto(orderUrl + '/invoice');
  ok('invoice shows barcode and contact', (await admin.locator('svg.barcode').count()) === 1 && (await admin.content()).includes('WhatsApp: 0339-2406479'));
  await admin.screenshot({ path: OUT + '/invoice.png', fullPage: true });
  const pdf = await admin.context().request.get(orderUrl + '/invoice-pdf');
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
  await adminAct('Mark as delivered').catch(() => {});
  ok('pick-up order cannot be marked delivered', (await admin.locator('button:has-text("Mark as delivered")').count()) === 0);
  await adminAct('Cancel order'); await admin.waitForLoadState();
  ok('unpaid order can be cancelled', (await admin.locator('.admin-order-head').innerText()).includes('Cancelled'));

  // --- Books handed over before any payment: stock comes off then, and not again at payment --------
  await admin.goto(B + '/admin/books/orders/new?q=0311'); await admin.click('a:has-text("DEMO Pickup Only")'); await admin.waitForLoadState();
  await admin.fill('#' + await admin.locator('tr', { hasText: 'DEMO Colours and Shapes' }).locator('input').getAttribute('id'), '2');
  await admin.click('button:has-text("Place order")'); await admin.waitForLoadState();
  const thirdNo = Number((admin.url().match(/orders\/(\d+)/) || [])[1]);
  await adminAct('Mark as collected'); await admin.waitForLoadState();
  ok('collected before payment takes stock (30-2=28)', (await stockOf('DEMO Colours and Shapes')) === 28);
  await admin.goto(B + '/admin/books/orders/' + thirdNo);
  ok('no backwards status buttons', (await admin.locator('button:has-text("Confirm order")').count()) === 0);
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


  // --- Returns per customer (owner's decision, 20 November 2026) -------------------------------------
  const pickCash = (sel) => admin.evaluate((id) => { const s = document.getElementById(id); const o = [...s.options].find(x => x.text.includes('DEMO Office cash')); s.value = o.value; }, sel);
  await admin.goto(B + '/admin/books/orders/' + thirdNo);
  ok('order page has no return form, only a link to the customer', (await admin.locator('button:has-text("Record return")').count()) === 0);
  const returnUrl = B + await admin.locator('a:has-text("Record a return for")').getAttribute('href');
  const accountUrl = returnUrl.replace(/\/return$/, '');
  const rq = async (name, v) => admin.locator('tr', { hasText: name }).locator('input').fill(String(v));
  await admin.goto(returnUrl); await rq('DEMO Colours and Shapes', 3); await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  ok('cannot return more than the customer has', (await admin.content()).includes('at most 2 can come back'));
  ok('nothing changed when refused', (await stockOf('DEMO Colours and Shapes')) === 28);
  await admin.goto(returnUrl); await rq('DEMO Colours and Shapes', 1); await admin.check('#ch-refund'); await admin.fill('#ReturnNote', 'DEMO damaged cover');
  await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  const rn = Number((admin.url().match(/rn=RN-(\d+)/) || [])[1]);
  ok('return recorded ' + rn, rn >= 101 && (await admin.locator('[role=status]').first().innerText()).includes('RN-' + rn));
  ok('returned book back in stock (28+1=29)', (await stockOf('DEMO Colours and Shapes')) === 29);
  await admin.goto(accountUrl + '?view=returns');
  ok('refund waiting to be paid (Rs. 220)', (await admin.locator('#refunds').innerText()).includes('Rs. 220'));
  await admin.selectOption('#m-' + rn, 'cash'); await pickCash('a-' + rn); await admin.click('#refunds button:has-text("Mark as paid")'); await admin.waitForLoadState();
  ok('refund recorded as paid', (await admin.content()).includes('Refund recorded as paid') && (await admin.locator('#refunds').count()) === 0);
  await admin.goto(B + '/admin/books/returns/' + rn);
  ok('return note page', (await admin.content()).includes('RETURN NOTE') && (await admin.content()).includes('WhatsApp: 0339-2406479') && (await admin.content()).includes('BK-' + thirdNo));
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
  await adminAct('Mark as collected'); await admin.waitForLoadState();   // only received books can come back
  // M2: a refund is only paid beyond what the customer still owes on other orders (owes nothing else here).
  const tooEarly = new Date(Date.now() + 5 * 3600e3 + 3 * 86400e3).toISOString().slice(0, 10);
  await admin.goto(returnUrl); await rq('DEMO Alphabet Book', 2); await admin.fill('#ReturnedOn', tooEarly);
  await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  ok('return date in the future refused', (await admin.content()).includes('cannot be in the future'));
  await admin.goto(returnUrl); await rq('DEMO Alphabet Book', 2); await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  await admin.goto(B + '/admin/books/orders/' + fourthNo);
  ok('first return: value after discount 500, nothing paid back, balance 0', (await balanceOf()) === 0);
  await admin.goto(returnUrl); await rq('DEMO Alphabet Book', 2); await admin.check('#ch-refund'); await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  ok('second return: refund 500 = cash received (not 520)', (await admin.locator('#refunds').innerText()).includes('Rs. 500'));
  ok('all four books back in stock (25)', (await stockOf('DEMO Alphabet Book')) === 25);
  // Credit choice: the last Colours book comes back as credit.
  await admin.goto(returnUrl); await rq('DEMO Colours and Shapes', 1); await admin.check('#ch-credit'); await admin.click('button:has-text("Record return")'); await admin.waitForLoadState();
  await admin.goto(accountUrl + '?view=ledger');
  const summaryText = await admin.locator('.admin-summary').innerText();
  ok('credit kept for the next order (Rs. 220)', summaryText.includes('Rs. 220') && summaryText.includes('Credit for the next order'));
  const ledgerRows = admin.locator('.admin-ledger tbody tr');
  const lastBal = money(await ledgerRows.last().locator('td').last().innerText());
  const sumBal = (await admin.locator('.admin-summary .admin-stat').first().innerText());
  ok('ledger balance = summary balance (school owes Rs. 220 + refund due counted)', /-?Rs\. -?220/.test(sumBal) || sumBal.includes('Rs. -220'));
  ok('ledger last balance matches the summary', Math.abs(lastBal) === 220);
  await admin.screenshot({ path: OUT + '/customer-account.png', fullPage: true });
  const stmt = await admin.context().request.get(accountUrl + '?view=ledger&format=pdf'); ok('statement of account PDF', (await stmt.body()).slice(0, 5).toString() === '%PDF-');
  const stx = await admin.context().request.get(accountUrl + '?view=orders&format=xlsx'); ok('customer orders Excel', (await stx.body()).slice(0, 2).toString() === 'PK');
  // Messages written for the office to send by hand.
  await admin.goto(accountUrl + '?view=messages');
  const msgs = await admin.locator('#messages').innerText();
  ok('messages written (order placed, payment, return)', msgs.includes('Order placed') && msgs.includes('Payment received') && msgs.includes('Return received') && msgs.includes('Refund paid'));
  ok('messages never mention a discount', !/discount/i.test(msgs));
  await admin.locator('.admin-msg').first().locator('button:has-text("Mark as sent")').click(); await admin.waitForLoadState();
  ok('message marked as sent', (await admin.content()).includes('Message marked as sent'));
  // Credit limit: a new order above the limit is refused.
  await admin.goto(accountUrl.replace('customer-accounts', 'customers'));
  await admin.fill('#CreditLimit', '100'); await admin.fill('#CreditDays', '30'); await admin.click('button:has-text("Save")'); await admin.waitForLoadState();
  await admin.goto(B + '/admin/books/orders/new?q=0311'); await admin.click('a:has-text("DEMO Pickup Only")'); await admin.waitForLoadState();
  // owed now = -220 (credit); 3 × 220 = 660 less 220 credit = 440 more → 220 owed > limit 100
  await admin.fill('#' + await admin.locator('tr', { hasText: 'DEMO Colours and Shapes' }).locator('input').getAttribute('id'), '3');
  await admin.click('button:has-text("Place order")'); await admin.waitForLoadState();
  ok('order above the credit limit refused', (await admin.content()).includes('above the credit limit'));
  await admin.goto(accountUrl.replace('customer-accounts', 'customers')); await admin.fill('#CreditLimit', ''); await admin.click('button:has-text("Save")'); await admin.waitForLoadState();
  await admin.goto(B + '/admin/books/customer-accounts');
  ok('customer accounts list shows the number', /C-\d{4}/.test(await admin.locator('table').innerText()));
  await admin.goto(B + '/admin/books');
  ok('Today page: queues and work lists', (await admin.locator('.admin-queue').count()) === 4 && (await admin.locator('#topack').count()) === 1 && (await admin.locator('#ready').count()) === 1);

  // --- Reports ----------------------------------------------------------------------------------
  const today = new Date(Date.now() + 5 * 3600e3).toISOString().slice(0, 10);
  for (const kind of ['stock', 'sales-books', 'sales-customers', 'payments', 'unpaid', 'discounts', 'returns', 'profit', 'aging', 'campus']) {
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
  ok('stock report has the old system columns', /STOCK IN[\s\S]*STOCK OUT[\s\S]*STOCK LEFT/.test((await admin.locator('.admin-report thead').innerText()).toUpperCase()));
  ok('stock report shows Colours 30 (two returned)', /DEMO Colours and Shapes[^\n]*30/.test(await admin.locator('.admin-report').innerText()));
  const anon = await cust.context().request.get(B + '/admin/books/reports?kind=stock&format=xlsx', { maxRedirects: 0 });
  ok('reports not visible to others', anon.status() !== 200);

  await admin.goto(B + '/admin/books/orders'); await admin.screenshot({ path: OUT + '/admin-orders.png', fullPage: true });
  ok('orders table fits without sideways scrolling', await admin.evaluate(() => { const w = document.querySelector('.admin-table-wrap'); return w.scrollWidth <= w.clientWidth + 1; }));
  await admin.locator('tr.admin-row-link').first().locator('td').nth(3).click(); await admin.waitForLoadState();
  ok('clicking anywhere on a row opens the order', /\/admin\/books\/orders\/\d+$/.test(admin.url()));
  await cust.goto(B + '/books/orders'); await cust.locator('.books-history-item').first().click({ position: { x: 30, y: 60 } }); await cust.waitForLoadState();
  ok('clicking anywhere on a card opens the order', /\/books\/orders\/\d+$/.test(cust.url()));
  await cust.goto(B + '/books/orders'); await cust.screenshot({ path: OUT + '/m-my-orders.png', fullPage: true });
  // --- Customer area: dashboard, payments with own receipts, statement, return requests -------------
  const dashOld = await cust.context().request.get(B + '/books/dashboard', { maxRedirects: 0 });
  ok('old dashboard address opens New order', dashOld.status() === 302 && dashOld.headers()['location'].endsWith('/books/order'));
  await cust.goto(B + '/books/order');
  ok('New order shows still to pay, orders and value in one box', /still to pay/i.test(await cust.locator('.books-summary').innerText()) && /value/i.test(await cust.locator('.books-summary').innerText()));
  await cust.goto(B + '/books/more');
  ok('More shows the customer number and Return books', /C-\d{4}/.test(await cust.locator('.books-me').innerText()) && (await cust.locator('.books-menu a:has-text("Return books")').count()) === 1);
  await cust.screenshot({ path: OUT + '/m-more.png', fullPage: true });
  await cust.goto(B + '/books/payments');
  const payText = await cust.locator('main').innerText();
  ok('payments: still to pay, pay now and one recent list', /still to pay/i.test(payText) && (await cust.locator('.books-paynow summary').count()) <= 1 && (await cust.locator('.books-recent li').count()) >= 3 && !payText.includes('Payments you sent'));
  ok('my payments never say discount', !/discount/i.test(payText));
  await cust.goto(B + '/books/orders/' + payNo);
  const ownReceipt = await cust.locator('#payment a:has-text("View receipt")').first().getAttribute('href');
  const rOwn = await cust.context().request.get(B + ownReceipt); ok('customer can open their own receipt', rOwn.status() === 200);
  const other = await (await browser.newContext()).newPage();
  await other.goto(B + '/books'); await other.fill('#bk-mobile', '0311-7654321'); await other.click('button:has-text("Continue")'); await other.waitForLoadState();
  const rOther = await other.context().request.get(B + ownReceipt, { maxRedirects: 0 }); ok('another customer cannot open it', rOther.status() === 404);
  await cust.goto(B + '/books/payments'); await cust.screenshot({ path: OUT + '/m-payments.png', fullPage: true });
  // Return request: ask for 2 English Reader 1 as credit, office approves, 1 arrives.
  await cust.goto(B + '/books/returns');
  await cust.fill('#' + await cust.locator('.books-row', { hasText: 'DEMO English Reader 1' }).locator('input').getAttribute('id'), '99');
  ok('browser stops a quantity above what I have', !(await cust.locator('.books-row', { hasText: 'DEMO English Reader 1' }).locator('input').evaluate(i => i.checkValidity())));
  await cust.evaluate(() => document.querySelectorAll('input[max]').forEach(i => i.removeAttribute('max')));   // check the server too
  await Promise.all([cust.waitForNavigation(), cust.click('button:has-text("Send return request")')]);
  ok('cannot ask to return more than received', (await cust.content()).includes('cannot return more books than you received'));
  await cust.fill('#' + await cust.locator('.books-row', { hasText: 'DEMO English Reader 1' }).locator('input').getAttribute('id'), '2');
  await cust.check('input[name=choice][value=credit]'); await Promise.all([cust.waitForNavigation(), cust.click('button:has-text("Send return request")')]);
  ok('return request sent', (await cust.content()).includes('Your return request has been sent') && (await cust.content()).includes('Waiting for the office'));
  await cust.screenshot({ path: OUT + '/m-returns.png', fullPage: true });
  const reqNo = Number(((await cust.locator('.books-sent').innerText()).match(/RN-(\d+)/) || [])[1]);
  await admin.goto(B + '/admin/books/return-requests?status=requested');
  ok('office sees the request', (await admin.content()).includes('RN-' + reqNo));
  await admin.goto(B + '/admin/books/return-requests/' + reqNo); await admin.click('button:has-text("Approve")'); await admin.waitForLoadState();
  ok('request approved', (await admin.content()).includes('Approved'));
  await cust.goto(B + '/books/returns'); ok('customer sees approved', (await cust.content()).includes('please send the books'));
  const englishBefore = await stockOf('DEMO English Reader 1');
  await admin.goto(B + '/admin/books/return-requests/' + reqNo);
  await admin.fill('#r_' + (await admin.locator('tr', { hasText: 'DEMO English Reader 1' }).locator('input').getAttribute('id')).slice(2), '1');
  await admin.click('button:has-text("Books received")'); await admin.waitForLoadState();
  ok('books received and return recorded', (await admin.content()).includes('Books received and the return recorded'));
  await admin.screenshot({ path: OUT + '/admin-return-request.png', fullPage: true });
  ok('received book back in stock', (await stockOf('DEMO English Reader 1')) === englishBefore + 1);
  await cust.goto(B + '/books/returns'); ok('customer sees received with value', (await cust.content()).includes('Received') && (await cust.content()).includes('Value: Rs.'));
  const signed = t => (String(t).includes('-') || String(t).includes('\u2212') ? -1 : 1) * money(t);
  const sameBalance = async () => { await cust.goto(B + '/books/payments'); const due = money(await cust.locator('.books-due-value').innerText());
    await admin.goto(B + '/admin/books/customer-accounts'); await admin.locator('tr', { hasText: 'DEMO Customer (test)' }).locator('a').first().click(); await admin.waitForLoadState();
    const bal = signed(await admin.locator('.admin-summary .admin-stat').first().locator('.admin-stat-value').innerText()); return due === Math.max(0, bal); };
  ok('customer still to pay = office balance', await sameBalance());
  // --- Notes 41-46 and 51 (21 November 2026) -------------------------------------------------------
  await cust.goto(B + '/books/more');
  const cNo = ((await cust.locator('.books-me').innerText()).match(/C-\d+/) || [])[0];
  // Two new unpaid orders: the payment goes to the oldest unpaid orders first.
  await placeOrder([['DEMO Colours and Shapes', 3]]);
  const olderNo = no();
  await placeOrder([['DEMO Alphabet Book', 4]]);
  const newestNo = no();   // 4 x 260 (any credit is used by the older order first)
  await cust.goto(B + '/books/payments');
  const owedNow = money(await cust.locator('.books-due-value').innerText());
  await cust.click('.books-paynow summary');
  const mpBox = cust.locator('#make-payment');
  ok('pay now shows both bank details and cash, no amount box', (await mpBox.innerText()).includes('12345678901') && (await mpBox.locator('.books-pay-opt', { hasText: 'Cash' }).count()) === 1 && (await mpBox.locator('input[name=PayAmount]').count()) === 0);
  await cust.screenshot({ path: OUT + '/m-make-payment.png', fullPage: true });
  await mpBox.locator('.books-pay-opt', { hasText: 'DEMO Customer Bank' }).locator('input').check(); await cust.setInputFiles('#mp-file', JPEG);
  await go('#make-payment button:has-text("Send payment")');
  ok('balance payment sent', (await cust.content()).includes('used for your oldest unpaid order first') && (await cust.locator('.books-due-note').first().innerText()).includes('being checked'));
  ok('the waiting payment is in Recent', (await cust.locator('.books-recent').innerText()).includes('Payment being checked'));
  await cust.goto(B + '/books/orders/' + newestNo);
  ok('orders cannot be changed while the payment is checked', (await cust.locator('button:has-text("Cancel order")').count()) === 0);
  await admin.goto(B + '/admin/books/orders/' + newestNo);
  ok('office sees the balance payment on the order', /towards the balance/i.test(await admin.locator('#sent').innerText()));
  ok('office cannot record another payment meanwhile', (await admin.content()).includes('Please confirm or reject it before recording another payment'));
  await admin.goto(B + '/admin/books');
  ok('Books overview lists payments to check with the receipt', (await admin.locator('#sent .admin-sent-item').count()) >= 1 && (await admin.locator('#sent .admin-receipt-thumb img').count()) >= 1);
  await admin.screenshot({ path: OUT + '/admin-payments-to-check.png', fullPage: true });
  await admin.goto(B + '/admin/books/customer-accounts'); await admin.locator('tr', { hasText: 'DEMO Customer (test)' }).locator('a').first().click(); await admin.waitForLoadState();
  ok('confirm form shows what was still to pay, the box is empty', money(await admin.locator('#sent input[name=Amount]').getAttribute('placeholder')) === owedNow && (await admin.locator('#sent input[name=Amount]').inputValue()) === '');
  await admin.fill('#sent input[name=Amount]', String(owedNow + 500));
  await admin.locator('#sent button:has-text("Confirm payment")').click(); await admin.waitForLoadState();
  ok('more than owed is refused unless the office ticks credit ' + (await admin.locator('.admin-alert').allInnerTexts()).join(' | '), (await admin.content()).includes('more than the customer owes'));
  await admin.goto(B + '/admin/books/customer-accounts'); await admin.locator('tr', { hasText: 'DEMO Customer (test)' }).locator('a').first().click(); await admin.waitForLoadState();
  // The receipt shows Rs. 100 less than what was still to pay: the office enters what the receipt shows.
  await admin.fill('#sent input[name=Amount]', String(owedNow - 100));
  await admin.locator('#sent button:has-text("Confirm payment")').click(); await admin.waitForLoadState();
  ok('office confirmed the balance payment', (await admin.content()).includes('applied to the oldest unpaid orders first'));
  await admin.goto(B + '/admin/books/orders/' + newestNo);
  ok('newest order keeps the rest (Rs. 100)', money(await admin.locator('tr.is-total td').innerText()) === 100);
  await admin.goto(B + '/admin/books/orders/' + olderNo);
  ok('older order fully paid and confirmed', money(await admin.locator('tr.is-total td').innerText()) === 0 && (await admin.locator('.admin-order-head').innerText()).includes('Confirmed'));
  await cust.goto(B + '/books/payments');
  ok('customer now owes Rs. 100', money(await cust.locator('.books-due-value').innerText()) === 100);
  // Statement PDF.
  const custPdf = await cust.context().request.get(B + '/books/statement');
  const pdfBody = await custPdf.body();
  ok('statement downloads as PDF', custPdf.status() === 200 && custPdf.headers()['content-type'].includes('application/pdf') && pdfBody.slice(0, 4).toString() === '%PDF');
  ok('statement PDF never says discount', !/discount/i.test(pdfBody.toString('latin1')));
  const anonPdf = await (await browser.newContext()).request.get(B + '/books/statement', { maxRedirects: 0 });
  ok('statement needs the customer to be signed in', anonPdf.status() === 302);
  // Messages tab with filters.
  await admin.goto(B + '/admin/books');
  ok('tabs: Today, Orders, Customers, Books & stock, Reports, More (with Messages)', (await admin.locator('.admin-books-bar .admin-tabs > a').allInnerTexts()).join('|') === 'Today|Orders|Customers|Books & stock|Reports' && (await admin.locator('.admin-tabs-more a:has-text("Messages")').count()) === 1);
  await admin.goto(B + '/admin/books/messages?status=all&kind=payment_received&q=' + encodeURIComponent(cNo));
  const msgList = admin.locator('.admin-msg');
  ok('messages filtered by customer and kind', (await msgList.count()) >= 2 && (await admin.locator('.admin-msg p strong').allInnerTexts()).every(t => t === 'Payment received'));
  await admin.goto(B + '/admin/books/messages?q=' + encodeURIComponent('0311-0000000'));
  ok('filter with no match says so', (await admin.content()).includes('No messages match the filter'));
  await admin.goto(B + '/admin/books/messages?status=unsent&q=' + encodeURIComponent(cNo));
  const msgBefore = await msgList.count();
  ok('copy and WhatsApp buttons', (await msgList.first().locator('button:has-text("Copy")').count()) === 1 && /^https:\/\/wa\.me\/923001234567\?text=/.test(await msgList.first().locator('a:has-text("Open in WhatsApp")').getAttribute('href')));
  await msgList.first().locator('button:has-text("Mark as sent")').click(); await admin.waitForLoadState();
  ok('mark as sent keeps the filter', admin.url().includes('q=' + encodeURIComponent(cNo)) && (await msgList.count()) === msgBefore - 1);
  await admin.goto(B + '/admin/books/messages?status=sent&q=' + encodeURIComponent(cNo));
  ok('sent filter shows it', (await msgList.count()) >= 1);
  await admin.goto(B + '/admin/books/messages?from=2001-01-01&to=2001-01-31&status=all');
  ok('date filter', (await msgList.count()) === 0);
  await admin.screenshot({ path: OUT + '/admin-messages.png', fullPage: true });
  // Super Admin removes a cancelled order (archived, hidden everywhere); a paid order cannot be removed.
  await admin.goto(B + '/admin/books/orders/' + payNo);
  ok('no remove button on a paid order', (await admin.locator('button:has-text("Remove this order")').count()) === 0);
  await admin.goto(B + '/admin/books/orders/' + cancelNo);
  await admin.screenshot({ path: OUT + '/admin-remove-order.png', fullPage: true });
  await adminAct('Remove this order'); await admin.waitForLoadState();
  ok('cancelled order removed', (await admin.content()).includes('The cancelled order was removed'));
  ok('removed order not in the list', !(await admin.locator('.admin-table').innerText()).includes('BK-' + cancelNo));
  const gone = await admin.context().request.get(B + '/admin/books/orders/' + cancelNo); ok('removed order page is gone for the office', gone.status() === 404);
  const goneC = await cust.context().request.get(B + '/books/orders/' + cancelNo); ok('removed order page is gone for the customer', goneC.status() === 404);
  await cust.goto(B + '/books/orders'); ok('removed order not in my orders', !(await cust.locator('main').innerText()).includes('BK-' + cancelNo));
  ok('still to pay still equals the office balance', await sameBalance());
  ok('no script errors ' + errors.join(' | '), errors.length === 0);
  console.log(results.join('\n'));
  console.log((results.filter(r => r.startsWith('FAIL')).length === 0 ? 'ALL PASSED' : 'SOME FAILED') + ' (' + results.length + ' checks)');
  await browser.close();
  process.exit(results.some(r => r.startsWith('FAIL')) ? 1 : 0);
})().catch(e => { console.log(results.join('\n')); console.error(e); process.exit(1); });
