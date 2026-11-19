# Books: end-to-end tests of the money and stock rules

`books-e2e.js` drives a **local** copy of the website with Playwright and checks the rules of the books
system (owner's specification, November 2026):

- totals, line totals and preview; a customer cannot order more than the stock in hand;
- only registered mobile numbers can order; customers cannot see each other's orders;
- an order can be edited until a payment is recorded, and every edit is kept;
- part payments, discounts and the balance; amount + discount cannot be more than the balance;
- the payment method must match the account (cash/bank); "received by" is the signed-in user;
- stock is taken off at the **first** payment only, never below zero, and a payment is refused when stock is short;
- statuses (pick up → Collected, delivery → Delivered; no cancelling after a payment);
- invoice page with barcode, PDF download, barcode search, receipts visible to the admin only;
- returns: never more than was taken, books go back into stock, the money is paid back at once, printable return note;
- reports (stock, sales by book and by customer, payments, unpaid, discounts, returns, profit) on screen, as PDF and as Excel, admin only.

It uses **synthetic data only** (books and customers named "DEMO …", numbers 0300-1234567 / 0311-7654321)
and must never be run against the live website or a database with real data.

```
node books-e2e.js <base-url> <admin-email> <admin-password> <admin-totp-secret> <folder-for-screenshots> <a-test-jpeg>
```

The local database must have Database/21 applied and no books, book customers, orders or returns yet.
