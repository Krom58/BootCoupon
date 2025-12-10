using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using System.Data.Common;

namespace BootCoupon.Services
{
    public static class ReceiptNumberService
    {
        /// <summary>
        /// สร้างหมายเลขใบเสร็จรูปแบบ: {Prefix}{YY}{NNN}
        /// เช่น INV25361 → INV26001 (ขึ้นปี 2026)
        /// </summary>
        public static async Task<string> GenerateNextReceiptCodeAsync()
        {
            // Get current year (ค.ศ. 2 หลัก)
            var currentYear = DateTime.Now.Year;
            var currentYearCode = currentYear % 100; // เช่น 25 สำหรับ 2025

            // Quick connectivity check
            try
            {
                using (var testCtx = new CouponContext())
                {
                    var canConnect = await testCtx.Database.CanConnectAsync();
                    if (!canConnect)
                    {
                        throw new InvalidOperationException("Database is not available. Please check the server connection.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DB connectivity check failed: {ex.Message}");
                throw;
            }

            // Try stored procedure first
            try
            {
                using var context = new CouponContext();
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.usp_GetNextReceiptCodeWithYear";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                var pYear = cmd.CreateParameter();
                pYear.ParameterName = "@YearCode";
                pYear.Value = currentYearCode;
                pYear.DbType = System.Data.DbType.Int32;
                cmd.Parameters.Add(pYear);

                var pMachine = cmd.CreateParameter();
                pMachine.ParameterName = "@MachineId";
                pMachine.Value = Environment.MachineName ?? "";
                pMachine.DbType = System.Data.DbType.String;
                cmd.Parameters.Add(pMachine);

                var obj = await cmd.ExecuteScalarAsync();
                if (obj == null) throw new InvalidOperationException("Stored procedure returned no value.");

                var code = obj.ToString();
                if (string.IsNullOrEmpty(code)) throw new InvalidOperationException("Stored procedure returned empty receipt code.");

                Debug.WriteLine($"Created receipt code (proc): {code}");
                return code!;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stored proc call failed: {ex.Message}");
            }

            // Fallback: table-based logic
            try
            {
                using var context = new CouponContext();

                // ===== ขั้นตอนที่ 1: ลองดึงหมายเลขที่ยกเลิกกลับมา =====
                var recycledCode = await context.CanceledReceiptNumbers
                    .Where(c => c.OwnerMachineId == Environment.MachineName)
                    .Where(c => c.ReceiptCode.Substring(3, 2) == currentYearCode.ToString("D2")) // กรองปี
                    .OrderBy(c => c.CanceledDate)
                    .FirstOrDefaultAsync();

                if (recycledCode != null)
                {
                    var code = recycledCode.ReceiptCode;
                    context.CanceledReceiptNumbers.Remove(recycledCode);
                    await context.SaveChangesAsync();

                    Debug.WriteLine($"♻️ Recycled receipt code: {code}");
                    return code;
                }

                // ===== ขั้นตอนที่ 2: สร้างหมายเลขใหม่ =====
                var numberManager = await context.ReceiptNumberManagers.FirstOrDefaultAsync();

                if (numberManager == null)
                {
                    numberManager = new ReceiptNumberManager
                    {
                        Prefix = "INV",
                        CurrentNumber = 1,
                        YearCode = currentYearCode,
                        UpdatedBy = Environment.MachineName
                    };
                    context.ReceiptNumberManagers.Add(numberManager);
                    await context.SaveChangesAsync();
                }

                if (numberManager.YearCode != currentYearCode)
                {
                    Debug.WriteLine($"Year changed: {numberManager.YearCode} → {currentYearCode} - Resetting number to 1");
                    numberManager.YearCode = currentYearCode;
                    numberManager.CurrentNumber = 1;
                }

                string nextCode = $"{numberManager.Prefix}{numberManager.YearCode:D2}{numberManager.CurrentNumber:D3}";

                numberManager.CurrentNumber++;
                numberManager.LastUpdated = DateTime.Now;
                numberManager.UpdatedBy = Environment.MachineName;

                await context.SaveChangesAsync();

                Debug.WriteLine($"Created receipt code: {nextCode}");
                return nextCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fallback generator failed: {ex.Message}");
                throw new InvalidOperationException("ไม่สามารถสร้างหมายเลขใบเสร็จได้", ex);
            }
        }

        public static async Task RecycleReceiptCodeAsync(string receiptCode, string reason = "Receipt canceled")
        {
            try
            {
                using (var context = new CouponContext())
                {
                    var exists = await context.CanceledReceiptNumbers
                        .AnyAsync(c => c.ReceiptCode == receiptCode);

                    if (!exists)
                    {
                        var canceledNumber = new CanceledReceiptNumber
                        {
                            ReceiptCode = receiptCode,
                            Reason = reason,
                            CanceledDate = DateTime.Now,
                            OwnerMachineId = Environment.MachineName
                        };

                        context.CanceledReceiptNumbers.Add(canceledNumber);
                        await context.SaveChangesAsync();

                        Debug.WriteLine($"เก็บหมายเลข {receiptCode} เพื่อนำมาใช้ใหม่");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการเก็บหมายเลข: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ดึงปี ค.ศ. จากรหัสใบเสร็จ (ตัวที่ 4-5 หลัง Prefix)
        /// เช่น INV25361 → 25
        /// </summary>
        public static int GetYearCodeFromReceiptCode(string receiptCode, string prefix = "INV")
        {
            if (string.IsNullOrEmpty(receiptCode) || receiptCode.Length < prefix.Length + 2)
                return 0;

            var yearPart = receiptCode.Substring(prefix.Length, 2);
            return int.TryParse(yearPart, out var year) ? year : 0;
        }

        /// <summary>
        /// ดึง Running Number จากรหัสใบเสร็จ (หลังปี)
        /// เช่น INV25361 → 361
        /// </summary>
        public static int GetRunningNumberFromReceiptCode(string receiptCode, string prefix = "INV")
        {
            if (string.IsNullOrEmpty(receiptCode) || receiptCode.Length < prefix.Length + 3)
                return 0;

            var numPart = receiptCode.Substring(prefix.Length + 2);
            return int.TryParse(numPart, out var num) ? num : 0;
        }
    }
}