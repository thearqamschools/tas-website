using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheArqamSchools.Web.Models
{
    /// <summary>A book in the books system (dbo.book_items). Prices and stock are entered by the office.</summary>
    public class BookItem
    {
        public Guid Id { get; set; }
        public string ClassName { get; set; }
        public string Name { get; set; }
        public int Stock { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        /// <summary>"books" or "stationery" (Database/24).</summary>
        public string Category { get; set; }
        /// <summary>Office only (Database/23): never shown to customers.</summary>
        public string Vendor { get; set; }
        public decimal? RetailPrice { get; set; }
        public decimal? DiscountPercent { get; set; }
        /// <summary>Low-stock warning when stock is at or below this; null = the default (<see cref="BookFormat.DefaultReorderLevel"/>).</summary>
        public int? ReorderLevel { get; set; }
        public int WarnAt { get { return ReorderLevel ?? BookFormat.DefaultReorderLevel; } }
    }

    /// <summary>One stock change of a book (dbo.book_stock_moves).</summary>
    public class BookStockMove
    {
        public string Kind { get; set; }
        public int Change { get; set; }
        public int StockAfter { get; set; }
        public DateTime MovedOn { get; set; }
        public string Note { get; set; }
        public long? OrderNo { get; set; }
        public string UserName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>A customer registered by the office (dbo.book_customers). The mobile number is the key.</summary>
    public class BookCustomer
    {
        public Guid Id { get; set; }
        public string Mobile { get; set; }
        public string Name { get; set; }
        public string CampusName { get; set; }
        public string Address { get; set; }
        public decimal Credit { get; set; }
        public bool IsActive { get; set; }
        public int OrderCount { get; set; }
        /// <summary>Customer number (Database/27), shown as C-1001.</summary>
        public long CustomerNo { get; set; }
        /// <summary>Most the customer may owe at once (null = no limit).</summary>
        public decimal? CreditLimit { get; set; }
        /// <summary>Days a handed-over order may stay unpaid before it shows as overdue (null = not tracked).</summary>
        public int? CreditDays { get; set; }
        public string CustomerLabel { get { return BookFormat.CustomerLabel(CustomerNo); } }
    }

    public class BookOrderLine
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public string ClassName { get; set; }
        public string ItemName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int ReturnedQty { get; set; }
        public decimal LineTotal { get { return UnitPrice * Quantity; } }
    }

    public class BookOrderEdit
    {
        public string Changes { get; set; }
        public decimal OldSubtotal { get; set; }
        public decimal NewSubtotal { get; set; }
        public bool ByCustomer { get; set; }
        public string UserName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>A book order (dbo.book_orders) with its lines.</summary>
    public class BookOrder
    {
        public Guid Id { get; set; }
        public long OrderNo { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        public string CampusName { get; set; }
        public string Fulfilment { get; set; }
        public string DeliveryAddress { get; set; }
        public string Status { get; set; }
        public string Source { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal CreditApplied { get; set; }
        public decimal PaidTotal { get; set; }
        public decimal ReturnedTotal { get; set; }
        public decimal RefundedTotal { get; set; }
        public int EditCount { get; set; }
        /// <summary>Payments the customer has sent online that are pending or confirmed (Database/26).</summary>
        public int SentPayments { get; set; }
        /// <summary>Payments the customer has sent that the office has not checked yet.</summary>
        public int PendingPayments { get; set; }
        public DateTimeOffset? StockTakenAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public IList<BookOrderLine> Lines { get; set; }
        public IList<BookOrderEdit> Edits { get; set; }

        /// <summary>What is still to be paid. Discounts and credit reduce it; returns that were paid back are added again.</summary>
        public decimal Balance { get { return Subtotal - ReturnedTotal - DiscountTotal - CreditApplied - PaidTotal + RefundedTotal; } }
        /// <summary>The amount the customer is asked for: the subtotal less credit. Discounts are never shown to customers.</summary>
        public decimal CustomerTotal { get { return Subtotal - CreditApplied; } }
        public string PaymentStatus
        {
            get
            {
                if (PaidTotal == 0 && DiscountTotal == 0) return "Pending";
                return Balance <= 0 ? "Paid" : "Part paid";
            }
        }
        /// <summary>
        /// Owner's rules (20 November 2026): the customer may change or cancel an order only while it is New and
        /// before they send a payment (a rejected payment does not count); never after a payment or discount.
        /// </summary>
        public bool CustomerCanEdit
        {
            get { return PaidTotal == 0 && DiscountTotal == 0 && StockTakenAt == null && Status == "new" && SentPayments == 0; }
        }
        /// <summary>The customer may send a payment while something is still to be paid and no earlier payment waits for the office.</summary>
        public bool CustomerCanPay
        {
            get { return Status != "cancelled" && Balance > 0 && PendingPayments == 0; }
        }
        /// <summary>What the customer sees: "Payment pending", "Payment sent" or the order status.</summary>
        public string CustomerStatusLabel
        {
            get
            {
                if (Status == "new" && PendingPayments > 0) return "Payment sent";
                if (Status == "new" && Balance > 0) return "Payment pending";
                return BookFormat.StatusLabel(Status);
            }
        }
        /// <summary>The office may change the books until any payment is recorded, while the order is New or Checked.</summary>
        public bool OfficeCanEdit
        {
            get { return PaidTotal == 0 && DiscountTotal == 0 && StockTakenAt == null && (Status == "new" || Status == "checked") && PendingPayments == 0; }
        }
        public string OrderLabel { get { return BookFormat.OrderLabel(OrderNo); } }
    }

    /// <summary>Shared wording and number formats for the books pages.</summary>
    public static class BookFormat
    {
        public const int DefaultReorderLevel = 10;

        public static string CategoryLabel(string c) { return c == "stationery" ? "Stationery" : "Books"; }

        /// <summary>The classes offered in the admin drop-down, in the owner's order.</summary>
        public static readonly string[] Classes =
        {
            "Play Group", "Nursery", "Prep", "Class 1", "Class 2", "Class 3", "Class 4", "Class 5", "Class 6", "Class 7",
            "Class 8", "Class 9", "Class 10", "Hifz Class", "All Classes"
        };

        /// <summary>Sale price = retail price less the discount percentage, rounded to paisa.</summary>
        public static decimal SaleFromRetail(decimal retail, decimal discountPercent)
        {
            return Math.Round(retail * (100m - discountPercent) / 100m, 2, MidpointRounding.AwayFromZero);
        }

        public static string OrderLabel(long orderNo) { return "BK-" + orderNo.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        public static string CustomerLabel(long customerNo) { return customerNo > 0 ? "C-" + customerNo.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""; }
        public static string ReturnLabel(long returnNo) { return "RN-" + returnNo.ToString(System.Globalization.CultureInfo.InvariantCulture); }

        public static string Money(decimal amount)
        {
            return "Rs. " + amount.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string StatusLabel(string status)
        {
            switch (status)
            {
                case "new": return "New";
                case "checked": return "Confirmed";
                case "packed": return "Packed";
                case "delivered": return "Delivered";
                case "collected": return "Collected";
                case "cancelled": return "Cancelled";
                default: return status;
            }
        }

        public static string FulfilmentLabel(string f) { return f == "delivery" ? "Delivery" : "Pick up"; }

        /// <summary>"House 12, Street 4, Samanabad, Faisalabad" becomes "House 1…, Faisalabad": enough to recognise it, not to copy it.</summary>
        public static string PartAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return "";
            var parts = address.Split(',');
            var last = parts[parts.Length - 1].Trim();
            var first = parts[0].Trim();
            var shortFirst = first.Length > 6 ? first.Substring(0, 6) + "…" : first;
            return parts.Length > 1 ? shortFirst + ", " + last : shortFirst;
        }
    }

    // ---- Admin forms ------------------------------------------------------------------

    public class BookItemForm
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Please enter the class, for example Class 1.")]
        [StringLength(60, ErrorMessage = "The class can be at most 60 characters.")]
        public string ClassName { get; set; }

        [Required(ErrorMessage = "Please enter the book name.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "The book name must be 2 to 200 characters.")]
        public string Name { get; set; }

        [Range(typeof(decimal), "0", "9999999", ErrorMessage = "Please enter the purchase price in rupees.")]
        public decimal PurchasePrice { get; set; }

        [Range(typeof(decimal), "0", "9999999", ErrorMessage = "Please enter the sale price in rupees.")]
        public decimal SalePrice { get; set; }

        public string Category { get; set; }

        [StringLength(150, ErrorMessage = "The vendor can be at most 150 characters.")]
        public string Vendor { get; set; }

        [Range(typeof(decimal), "0", "9999999", ErrorMessage = "Please enter the retail price in rupees.")]
        public decimal? RetailPrice { get; set; }

        [Range(typeof(decimal), "0", "100", ErrorMessage = "Please enter a discount from 0 to 100 per cent.")]
        public decimal? DiscountPercent { get; set; }

        [Range(0, 9999, ErrorMessage = "Please enter a number from 0 to 9999.")]
        public int SortOrder { get; set; }

        public bool IsActive { get; set; }
    }

    public class BookCustomerForm
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Please enter the mobile number, for example 0300-1234567.")]
        public string Mobile { get; set; }

        [Required(ErrorMessage = "Please enter the customer's name.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "The name must be 2 to 150 characters.")]
        public string Name { get; set; }

        [StringLength(150, ErrorMessage = "The campus name can be at most 150 characters.")]
        public string CampusName { get; set; }

        [StringLength(400, ErrorMessage = "The address can be at most 400 characters.")]
        public string Address { get; set; }

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "Please enter the credit limit in rupees, or leave it empty.")]
        public decimal? CreditLimit { get; set; }

        [Range(0, 3650, ErrorMessage = "Please enter the credit days (0 to 3650), or leave it empty.")]
        public int? CreditDays { get; set; }

        public long CustomerNo { get; set; }

        public bool IsActive { get; set; }
    }

    public class BookItemEditViewModel
    {
        public BookItemForm Form { get; set; }
        public bool IsNew { get; set; }
        public int Stock { get; set; }
        public IList<BookStockMove> Moves { get; set; }
        public IList<string> Classes { get; set; }
        public int? ReorderLevel { get; set; }
        public string Notice { get; set; }
        public bool CanEdit { get; set; }
    }

    /// <summary>The customer's order form: one row per active book with stock, grouped by class.</summary>
    public class BookOrderFormViewModel
    {
        public BookCustomer Customer { get; set; }
        public IList<BookItem> Items { get; set; }
        public IDictionary<Guid, int> Quantities { get; set; }
        public string Fulfilment { get; set; }
        /// <summary>When editing: the order being changed.</summary>
        public BookOrder Editing { get; set; }
        public IList<string> Errors { get; set; }
        public bool Preview { get; set; }
        /// <summary>"form", "preview" or "payment" (notes 57-59).</summary>
        public string Step { get; set; }
        /// <summary>Still to pay, orders and their value, shown at the top of New order (note 56).</summary>
        public TheArqamSchools.Web.Data.BookCustomerAccountRepository.Summary Summary { get; set; }
        public IList<BookAccount> Accounts { get; set; }
    }

    // ---- Part 2: accounts, payments, office order list ---------------------------------------

    /// <summary>A cash or bank account that book payments go into (dbo.book_accounts).</summary>
    public class BookAccount
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Details { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Bank details shown to customers when <see cref="ShowToCustomers"/> (Database/26).</summary>
        public string AccountTitle { get; set; }
        public string AccountNumber { get; set; }
        public string Iban { get; set; }
        public bool ShowToCustomers { get; set; }
    }

    /// <summary>A payment the customer sent online with a receipt (dbo.book_payment_submissions), waiting for the office.</summary>
    public class BookPaymentSubmission
    {
        public Guid Id { get; set; }
        /// <summary>Empty when the customer paid towards their whole balance (Database/28).</summary>
        public Guid? OrderId { get; set; }
        public long? OrderNo { get; set; }
        public Guid CustomerId { get; set; }
        public long CustomerNo { get; set; }
        public string CustomerLabel { get { return BookFormat.CustomerLabel(CustomerNo); } }
        /// <summary>Other payments of the same customer waiting for the office (a warning against counting one receipt twice).</summary>
        public int OtherWaiting { get; set; }
        /// <summary>Part of a balance payment that became credit when it was confirmed.</summary>
        public decimal CreditAmount { get; set; }
        public Guid AccountId { get; set; }
        public string AccountName { get; set; }
        public decimal Amount { get; set; }
        public string ReceiptPath { get; set; }
        public string ReceiptMime { get; set; }
        public string Status { get; set; }
        public string RejectReason { get; set; }
        public string ReviewedByName { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string CustomerName { get; set; }
        public string StatusLabel
        {
            get { return Status == "confirmed" ? "Confirmed" : Status == "rejected" ? "Not accepted" : "Waiting for the office"; }
        }
    }

    public class BookPayment
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public decimal Discount { get; set; }
        public string Method { get; set; }
        public string AccountName { get; set; }
        public string ReceivedByName { get; set; }
        public DateTime ReceivedOn { get; set; }
        public string ReceiptPath { get; set; }
        public string ReceiptMime { get; set; }
        public string Note { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>What the office types when recording a payment. "Received by" is never posted: it is the signed-in user.</summary>
    public class BookPaymentForm
    {
        public decimal Amount { get; set; }
        public decimal Discount { get; set; }
        public string Method { get; set; }
        public Guid? AccountId { get; set; }
        public DateTime ReceivedOn { get; set; }
        public string Note { get; set; }
    }

    public class BookOrderFilter
    {
        public string Status { get; set; }
        public string Payment { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string Query { get; set; }
    }

    public class BookCounts
    {
        public int NewOrders { get; set; }
        public int PaymentsToCheck { get; set; }
        public int UnpaidOrders { get; set; }
        public int LastDay { get; set; }
    }

    public class BookOrderAdminViewModel
    {
        public BookOrder Order { get; set; }
        public IList<BookPayment> Payments { get; set; }
        public IList<BookAccount> Accounts { get; set; }
        public IList<BookReturn> Returns { get; set; }
        public IList<BookPaymentSubmission> Submissions { get; set; }
        public BookPaymentForm PaymentForm { get; set; }
        public string Notice { get; set; }
        public string Error { get; set; }
        public bool CanOrders { get; set; }
        public bool CanPay { get; set; }
        /// <summary>Only a Super Admin may remove a cancelled order (owner's decision, 21 November 2026).</summary>
        public bool CanRemove { get; set; }
    }

    // ---- Part 3: returns and reports ------------------------------------------------------------

    public class BookReturn
    {
        public Guid Id { get; set; }
        public long ReturnNo { get; set; }
        public Guid? OrderId { get; set; }
        public long? OrderNo { get; set; }
        public Guid CustomerId { get; set; }
        public long CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        public decimal Amount { get; set; }
        public decimal RefundAmount { get; set; }
        /// <summary>Part of the return's money that went to the customer's credit (Database/27).</summary>
        public decimal CreditAmount { get; set; }
        public DateTimeOffset? RefundPaidAt { get; set; }
        /// <summary>The orders the returned books came from, for example "BK-1004, BK-1006".</summary>
        public string OrderLabels { get; set; }
        public bool RefundPending { get { return RefundAmount > 0 && RefundPaidAt == null; } }
        public string RefundMethod { get; set; }
        public string AccountName { get; set; }
        public DateTime RefundedOn { get; set; }
        public string Note { get; set; }
        public string CreatedByName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public IList<BookReturnLine> Lines { get; set; }
        public string ReturnLabel { get { return "RN-" + ReturnNo.ToString(System.Globalization.CultureInfo.InvariantCulture); } }
    }

    public class BookReturnLine
    {
        public long OrderNo { get; set; }
        public decimal? NetAmount { get; set; }
        public string ClassName { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get { return UnitPrice * Quantity; } }
    }

    public class BookCustomerAccountViewModel
    {
        public BookCustomer Customer { get; set; }
        public TheArqamSchools.Web.Data.BookCustomerAccountRepository.Summary Summary { get; set; }
        public string View { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string Session { get; set; }
        public TheArqamSchools.Web.Infrastructure.ReportTable Report { get; set; }
        public IList<TheArqamSchools.Web.Data.BookMessageRepository.Message> Messages { get; set; }
        public IList<BookReturn> PendingRefunds { get; set; }
        /// <summary>Payments the customer sent towards the whole balance (Database/28); the newest ten.</summary>
        public IList<BookPaymentSubmission> Submissions { get; set; }
        public IList<BookAccount> Accounts { get; set; }
        public bool CanPay { get; set; }
        public bool CanOrders { get; set; }
        public string Notice { get; set; }
    }
}
