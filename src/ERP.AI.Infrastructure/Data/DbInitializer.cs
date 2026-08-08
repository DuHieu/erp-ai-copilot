using ERP.AI.Core.Entities;
using ERP.AI.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(ErpDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Customers.AnyAsync())
        {
            return; // DB already seeded
        }

        // 1. Seed Customers (20 Customers)
        var customers = new List<Customer>
        {
            new() { Id = 1, CustomerCode = "CUS001", CustomerName = "MAEDA CORPORATION", IsActive = true },
            new() { Id = 2, CustomerCode = "CUS002", CustomerName = "ABC COMPANY LIMITED", IsActive = true },
            new() { Id = 3, CustomerCode = "CUS003", CustomerName = "VINHOMES JOINT STOCK COMPANY", IsActive = true },
            new() { Id = 4, CustomerCode = "CUS004", CustomerName = "TAISEI CORPORATION", IsActive = true },
            new() { Id = 5, CustomerCode = "CUS005", CustomerName = "COTECCONS CONSTRUCTION JSC", IsActive = true },
            new() { Id = 6, CustomerCode = "CUS006", CustomerName = "HOA BINH CONSTRUCTION GROUP", IsActive = true },
            new() { Id = 7, CustomerCode = "CUS007", CustomerName = "SHINRYO VIETNAM CO., LTD", IsActive = true },
            new() { Id = 8, CustomerCode = "CUS008", CustomerName = "AN PHONG CONSTRUCTION", IsActive = true },
            new() { Id = 9, CustomerCode = "CUS009", CustomerName = "DELTA CIVIL AND INDUSTRIAL CONSTRUCTION", IsActive = true },
            new() { Id = 10, CustomerCode = "CUS010", CustomerName = "UNICONS INVESTMENT CONSTRUCTION", IsActive = true },
            new() { Id = 11, CustomerCode = "CUS011", CustomerName = "RICONS CONSTRUCTION INVESTMENT", IsActive = true },
            new() { Id = 12, CustomerCode = "CUS012", CustomerName = "TAKAZAWA INDUSTRIAL VIETNAM", IsActive = true },
            new() { Id = 13, CustomerCode = "CUS013", CustomerName = "SAMSUNG ENGINEERING VIETNAM", IsActive = true },
            new() { Id = 14, CustomerCode = "CUS014", CustomerName = "DOOSAN ENERBILITY VIETNAM", IsActive = true },
            new() { Id = 15, CustomerCode = "CUS015", CustomerName = "SUN GROUP CORPORATION", IsActive = true },
            new() { Id = 16, CustomerCode = "CUS016", CustomerName = "NOVALAND GROUP", IsActive = true },
            new() { Id = 17, CustomerCode = "CUS017", CustomerName = "DAT XANH GROUP", IsActive = true },
            new() { Id = 18, CustomerCode = "CUS018", CustomerName = "PHAT DAT REAL ESTATE DEVELOPMENT", IsActive = true },
            new() { Id = 19, CustomerCode = "CUS019", CustomerName = "KHANG DIEN HOUSE TRADING", IsActive = true },
            new() { Id = 20, CustomerCode = "CUS020", CustomerName = "CAPITALAND VIETNAM", IsActive = true }
        };

        await context.Customers.AddRangeAsync(customers);
        await context.SaveChangesAsync();

        // 2. Seed Items (35 Items with varying stock)
        var items = new List<Item>
        {
            new() { Id = 1, ItemCode = "ITEM001", ItemName = "Steel Plate 10mm", Unit = "Sheet", CurrentStock = 80, MinimumStock = 100 },
            new() { Id = 2, ItemCode = "ITEM002", ItemName = "Rebar Steel 16mm", Unit = "Ton", CurrentStock = 250, MinimumStock = 150 },
            new() { Id = 3, ItemCode = "ITEM003", ItemName = "Portland Cement PCB40", Unit = "Bag", CurrentStock = 40, MinimumStock = 200 },
            new() { Id = 4, ItemCode = "ITEM004", ItemName = "Ready-Mix Concrete C30", Unit = "m3", CurrentStock = 500, MinimumStock = 300 },
            new() { Id = 5, ItemCode = "ITEM005", ItemName = "Aluminum Structural Beam 50x100", Unit = "Meter", CurrentStock = 15, MinimumStock = 60 },
            new() { Id = 6, ItemCode = "ITEM006", ItemName = "Galvanized Iron Pipe 2 inch", Unit = "Piece", CurrentStock = 120, MinimumStock = 100 },
            new() { Id = 7, ItemCode = "ITEM007", ItemName = "Safety Helmet Yellow", Unit = "Piece", CurrentStock = 18, MinimumStock = 50 },
            new() { Id = 8, ItemCode = "ITEM008", ItemName = "High Visibility Vest", Unit = "Piece", CurrentStock = 25, MinimumStock = 50 },
            new() { Id = 9, ItemCode = "ITEM009", ItemName = "Scaffolding Frame 1.7m", Unit = "Set", CurrentStock = 90, MinimumStock = 120 },
            new() { Id = 10, ItemCode = "ITEM010", ItemName = "Plywood Panel 18mm", Unit = "Sheet", CurrentStock = 30, MinimumStock = 100 },
            new() { Id = 11, ItemCode = "ITEM011", ItemName = "Electrical Copper Cable 3x4mm2", Unit = "Meter", CurrentStock = 850, MinimumStock = 500 },
            new() { Id = 12, ItemCode = "ITEM012", ItemName = "LED Floodlight 100W", Unit = "Piece", CurrentStock = 12, MinimumStock = 30 },
            new() { Id = 13, ItemCode = "ITEM013", ItemName = "Welding Rod E6013", Unit = "Box", CurrentStock = 5, MinimumStock = 25 },
            new() { Id = 14, ItemCode = "ITEM014", ItemName = "Anti-Rust Primer Paint 20L", Unit = "Bucket", CurrentStock = 8, MinimumStock = 20 },
            new() { Id = 15, ItemCode = "ITEM015", ItemName = "Epoxy Floor Coating 18kg", Unit = "Set", CurrentStock = 45, MinimumStock = 40 }
        };

        for (int i = 16; i <= 35; i++)
        {
            items.Add(new Item
            {
                Id = i,
                ItemCode = $"ITEM{i:D3}",
                ItemName = $"Construction Supply Component #{i}",
                Unit = "Unit",
                CurrentStock = (i % 3 == 0) ? i * 2 : i * 15,
                MinimumStock = i * 10
            });
        }

        await context.Items.AddRangeAsync(items);
        await context.SaveChangesAsync();

        // 3. Seed Projects (12 Projects)
        var projects = new List<Project>
        {
            new() { Id = 1, ProjectCode = "PRJ001", ProjectName = "MAEDA High-Tech Factory", BudgetAmount = 10_000_000_000m, ActualCost = 10_820_000_000m, Status = "In Progress" },
            new() { Id = 2, ProjectCode = "PRJ002", ProjectName = "Vinhomes Central Park Phase 3", BudgetAmount = 55_000_000_000m, ActualCost = 48_500_000_000m, Status = "In Progress" },
            new() { Id = 3, ProjectCode = "PRJ003", ProjectName = "Taisei Commercial Complex", BudgetAmount = 30_000_000_000m, ActualCost = 28_900_000_000m, Status = "In Progress" },
            new() { Id = 4, ProjectCode = "PRJ004", ProjectName = "Metro Line 3 Extension Infrastructure", BudgetAmount = 25_000_000_000m, ActualCost = 27_500_000_000m, Status = "Over Budget" },
            new() { Id = 5, ProjectCode = "PRJ005", ProjectName = "Samsung R&D Center Substation", BudgetAmount = 12_000_000_000m, ActualCost = 11_800_000_000m, Status = "Completed" },
            new() { Id = 6, ProjectCode = "PRJ006", ProjectName = "Sun World Resort Structural Frame", BudgetAmount = 40_000_000_000m, ActualCost = 43_200_000_000m, Status = "Over Budget" },
            new() { Id = 7, ProjectCode = "PRJ007", ProjectName = "Novaland Riverfront Towers", BudgetAmount = 60_000_000_000m, ActualCost = 54_000_000_000m, Status = "In Progress" },
            new() { Id = 8, ProjectCode = "PRJ008", ProjectName = "CapitaLand Luxury Residences", BudgetAmount = 35_000_000_000m, ActualCost = 36_800_000_000m, Status = "Over Budget" },
            new() { Id = 9, ProjectCode = "PRJ009", ProjectName = "Shinryo Cleanroom Facility", BudgetAmount = 8_500_000_000m, ActualCost = 7_900_000_000m, Status = "Completed" },
            new() { Id = 10, ProjectCode = "PRJ010", ProjectName = "Cottecons Logistics Warehouse", BudgetAmount = 18_000_000_000m, ActualCost = 17_400_000_000m, Status = "In Progress" },
            new() { Id = 11, ProjectCode = "PRJ011", ProjectName = "Hoa Binh Hospital Wing B", BudgetAmount = 22_000_000_000m, ActualCost = 23_100_000_000m, Status = "Over Budget" },
            new() { Id = 12, ProjectCode = "PRJ012", ProjectName = "Doosan Thermal Power Piping", BudgetAmount = 15_000_000_000m, ActualCost = 14_200_000_000m, Status = "In Progress" }
        };

        await context.Projects.AddRangeAsync(projects);
        await context.SaveChangesAsync();

        // 4. Seed Invoices (110 Invoices)
        var baseDate = new DateTime(2026, 8, 9);
        var invoices = new List<Invoice>();
        int invoiceId = 1;

        // CUS001 MAEDA (High outstanding balance)
        invoices.Add(new Invoice { Id = invoiceId++, InvoiceNo = "INV-2026-001", CustomerId = 1, InvoiceDate = baseDate.AddDays(-60), DueDate = baseDate.AddDays(-30), Currency = "VND", TotalAmount = 350_000_000m, PaidAmount = 0, Status = InvoiceStatus.Overdue });
        invoices.Add(new Invoice { Id = invoiceId++, InvoiceNo = "INV-2026-002", CustomerId = 1, InvoiceDate = baseDate.AddDays(-45), DueDate = baseDate.AddDays(-15), Currency = "VND", TotalAmount = 270_000_000m, PaidAmount = 100_000_000m, Status = InvoiceStatus.Overdue });
        invoices.Add(new Invoice { Id = invoiceId++, InvoiceNo = "INV-2026-003", CustomerId = 1, InvoiceDate = baseDate.AddDays(-20), DueDate = baseDate.AddDays(10), Currency = "VND", TotalAmount = 330_000_000m, PaidAmount = 0, Status = InvoiceStatus.Open });

        // CUS002 ABC (High balance)
        invoices.Add(new Invoice { Id = invoiceId++, InvoiceNo = "INV-2026-004", CustomerId = 2, InvoiceDate = baseDate.AddDays(-50), DueDate = baseDate.AddDays(-20), Currency = "VND", TotalAmount = 450_000_000m, PaidAmount = 100_000_000m, Status = InvoiceStatus.Overdue });
        invoices.Add(new Invoice { Id = invoiceId++, InvoiceNo = "INV-2026-005", CustomerId = 2, InvoiceDate = baseDate.AddDays(-30), DueDate = baseDate.AddDays(0), Currency = "VND", TotalAmount = 270_000_000m, PaidAmount = 0, Status = InvoiceStatus.Open });

        // CUS003 VINHOMES
        invoices.Add(new Invoice { Id = invoiceId++, InvoiceNo = "INV-2026-006", CustomerId = 3, InvoiceDate = baseDate.AddDays(-70), DueDate = baseDate.AddDays(-40), Currency = "VND", TotalAmount = 500_000_000m, PaidAmount = 100_000_000m, Status = InvoiceStatus.Overdue });
        invoices.Add(new Invoice { Id = invoiceId++, InvoiceNo = "INV-2026-007", CustomerId = 3, InvoiceDate = baseDate.AddDays(-15), DueDate = baseDate.AddDays(15), Currency = "VND", TotalAmount = 150_000_000m, PaidAmount = 50_000_000m, Status = InvoiceStatus.Partial });

        // Generate remaining 103 invoices across all 20 customers
        var random = new Random(42);
        for (int i = 8; i <= 110; i++)
        {
            int custId = random.Next(1, 21);
            int daysAgo = random.Next(5, 120);
            decimal total = random.Next(50, 600) * 1_000_000m;
            int statusRoll = random.Next(1, 10);
            decimal paid = 0;
            InvoiceStatus status;
            DateTime invDate = baseDate.AddDays(-daysAgo);
            DateTime dueDate = invDate.AddDays(30);

            if (statusRoll <= 4)
            {
                paid = total;
                status = InvoiceStatus.Paid;
            }
            else if (dueDate < baseDate)
            {
                paid = (statusRoll % 2 == 0) ? total * 0.3m : 0m;
                status = InvoiceStatus.Overdue;
            }
            else if (statusRoll == 5 || statusRoll == 6)
            {
                paid = total * 0.4m;
                status = InvoiceStatus.Partial;
            }
            else
            {
                paid = 0;
                status = InvoiceStatus.Open;
            }

            invoices.Add(new Invoice
            {
                Id = invoiceId++,
                InvoiceNo = $"INV-2026-{i:D3}",
                CustomerId = custId,
                InvoiceDate = invDate,
                DueDate = dueDate,
                Currency = "VND",
                TotalAmount = total,
                PaidAmount = paid,
                Status = status
            });
        }

        await context.Invoices.AddRangeAsync(invoices);
        await context.SaveChangesAsync();

        // 5. Seed Sales Transactions (210 Transactions)
        var sales = new List<Sale>();
        int saleId = 1;
        var julyStart = new DateTime(2026, 7, 1);

        // Seed 158 transactions specifically in July 2026 to match sample prompt expectations (~12.5 Billion VND total)
        for (int i = 1; i <= 158; i++)
        {
            int custId = ((i - 1) % 20) + 1;
            int dayOffset = (i % 31);
            sales.Add(new Sale
            {
                Id = saleId++,
                DocumentNo = $"SAL-202607-{i:D3}",
                CustomerId = custId,
                TransactionDate = julyStart.AddDays(dayOffset).AddHours(i % 12),
                Amount = 79_113_924m, // 158 * 79,113,924 ~= 12,500,000,000 VND
                Currency = "VND"
            });
        }

        // Seed remaining transactions in June & August 2026
        var juneStart = new DateTime(2026, 6, 1);
        var augStart = new DateTime(2026, 8, 1);

        for (int i = 1; i <= 30; i++)
        {
            sales.Add(new Sale
            {
                Id = saleId++,
                DocumentNo = $"SAL-202606-{i:D3}",
                CustomerId = (i % 20) + 1,
                TransactionDate = juneStart.AddDays(i % 30),
                Amount = 350_000_000m,
                Currency = "VND"
            });
        }

        for (int i = 1; i <= 22; i++)
        {
            sales.Add(new Sale
            {
                Id = saleId++,
                DocumentNo = $"SAL-202608-{i:D3}",
                CustomerId = (i % 20) + 1,
                TransactionDate = augStart.AddDays(i % 8),
                Amount = 400_000_000m,
                Currency = "VND"
            });
        }

        await context.Sales.AddRangeAsync(sales);
        await context.SaveChangesAsync();
    }
}
