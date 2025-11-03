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
        /// สร้างหมายเลขใบเสร็จใหม่แบบ Thread-Safe
        /// เก็บ logic ในฐานข้อมูลโดยเรียก stored procedure dbo.usp_GetNextReceiptCode
        /// Stored proc จะพยายาม pop หมายเลขจาก CanceledReceiptNumbers แบบ atomic
        /// หรือเรียก NEXT VALUE FOR dbo.ReceiptNumbers ถ้าไม่มีเลขรีไซเคิล
        /// </summary>
        public static async Task<string> GenerateNextReceiptCodeAsync()
        {
            // Quick connectivity check: ensure DB is reachable before attempting allocation.
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
                // Re-throw to let caller handle (no silent fallback here)
                throw;
            }

            // Try calling stored procedure in DB that returns the next receipt code atomically
            try
            {
                using var context = new CouponContext();
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.usp_GetNextReceiptCode"; // proc name
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                // Pass MachineId so proc can return machine-owned canceled numbers first
                var p = cmd.CreateParameter();
                p.ParameterName = "@MachineId";
                p.Value = Environment.MachineName ?? "";
                p.DbType = System.Data.DbType.String;
                cmd.Parameters.Add(p);

                var obj = await cmd.ExecuteScalarAsync();
                if (obj == null) throw new InvalidOperationException("Stored procedure dbo.usp_GetNextReceiptCode returned no value.");

                var code = obj.ToString();
                if (string.IsNullOrEmpty(code)) throw new InvalidOperationException("Stored procedure returned empty receipt code.");

                Debug.WriteLine($"Created receipt code (proc): {code}");
                return code!;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stored proc call failed: {ex.Message}");
                // Fallback: use table-based ReceiptNumberManager as last resort (not ideal for concurrency)
            }

            // Fallback path (best-effort) - keep original table-based increment
            try
            {
                using var context = new CouponContext();
                var numberManager = await context.ReceiptNumberManagers.FirstOrDefaultAsync();
                if (numberManager == null)
                {
                    numberManager = new ReceiptNumberManager
                    {
                        Prefix = "INV",
                        CurrentNumber = 5001,
                        UpdatedBy = Environment.MachineName
                    };
                    context.ReceiptNumberManagers.Add(numberManager);
                    await context.SaveChangesAsync();
                }

                string nextCode = $"{numberManager.Prefix}{numberManager.CurrentNumber}";
                numberManager.CurrentNumber++;
                numberManager.LastUpdated = DateTime.Now;
                numberManager.UpdatedBy = Environment.MachineName;
                await context.SaveChangesAsync();

                Debug.WriteLine($"Created receipt code (fallback): {nextCode}");
                return nextCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fallback generator failed: {ex.Message}");
                throw new InvalidOperationException("ไม่สามารถสร้างหมายเลขใบเสร็จได้", ex);
            }
        }

        /// <summary>
        /// เก็บหมายเลขใบเสร็จที่ถูกยกเลิกเพื่อนำมาใช้ใหม่
        /// </summary>
        public static async Task RecycleReceiptCodeAsync(string receiptCode, string reason = "Receipt canceled")
        {
            try
            {
                using (var context = new CouponContext()) // ใช้ CouponContext จาก Shared
                {
                    // ตรวจสอบว่าหมายเลขนี้ไม่ได้ถูกเก็บไว้แล้ว
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

                        Debug.WriteLine($"เก็บหมายเลข {receiptCode} เพื่อนำมาใช้ใหม่ (Owner: {Environment.MachineName})");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการเก็บหมายเลขเพื่อรีไซเคิล: {ex.Message}");
                throw;
            }
        }
    }
}