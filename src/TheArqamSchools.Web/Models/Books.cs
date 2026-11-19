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
        /// <summary>A customer may change an order until any payment or discount is recorded, and while it is not finished.</summary>
        public bool CustomerCanEdit
        {
            get { return PaidTotal == 0 && DiscountTotal == 0 && StockTakenAt == null && (Status == "new" || Status == "checked"); }
        }
        public string OrderLabel { get { return BookFormat.OrderLabel(OrderNo); } }
    }

    /// <summary>Shared wording and number formats for the books pages.</summary>
    public static class BookFormat
    {
        public static string OrderLabel(long orderNo) { return "BK-" + orderNo.ToString(System.Globalization.CultureInfo.InvariantCulture); }

        public static string Money(decimal amount)
        {
            return "Rs. " + amount.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string StatusLabel(string status)
        {
            switch (status)
            {
                case "new": return "New";
                case "checked": return "Checked";
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

        public bool IsActive { get; set; }
    }

    public class BookItemEditViewModel
    {
        public BookItemForm Form { get; set; }
        public bool IsNew { get; set; }
        public int Stock { get; set; }
        public IList<BookStockMove> Moves { get; set; }
        public IList<string> Classes { get; set; }
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
    }
}
