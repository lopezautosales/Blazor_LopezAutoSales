﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace LopezAutoSales.Shared.Models
{
    public class Account
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public Sale Sale { get; set; }

        public bool IsPaid { get; set; }
        [Required]
        [Column(TypeName = "decimal(9,2)")]
        public decimal InitialDue { get; set; }
        public List<Payment> Payments { get; set; }
        [Required]
        [Column(TypeName = "decimal(9,2)")]
        public decimal MonthlyPayment { get; set; } = Dealership.MonthlyPayment;

        public decimal Balance()
        {
            return InitialDue - Payments.Sum(x => x.Amount);
        }

        public bool HasContractExpired()
        {
            if (ExpirationDate() <= DateTime.Now)
                return true;
            return false;
        }

        private int MonthsToPay()
        {
            if (MonthlyPayment == 0)
                return 0;
            return (int)Math.Ceiling(InitialDue / MonthlyPayment);
        }

        public decimal LateDue()
        {
            DateTime date = Sale.Date.AddMonths(1);
            decimal expected = 0;
            while (date < DateTime.Now && expected < InitialDue)
            {
                expected += MonthlyPayment;
                date = date.AddMonths(1);
            }
            if (expected > InitialDue)
                expected = InitialDue;
            return expected - Payments.Sum(x => x.Amount);
        }

        public DateTime ExpirationDate()
        {
            return Sale.Date.AddMonths(MonthsToPay());
        }
    }
}
