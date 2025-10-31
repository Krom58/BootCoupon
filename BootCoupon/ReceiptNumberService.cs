using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;

namespace BootCoupon.Services
{
    public static class ReceiptNumberService
    {
        /// <summary>
        /// สร้างหมายเลขใบเสร็จใหม่แบบ Thread-Safe
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

            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    using (var context = new CouponContext()) // ใช้ CouponContext จาก Shared
                    {
                        // ใช้ Transaction เพื่อป้องกัน Concurrent Access
                        using (var transaction = await context.Database.BeginTransactionAsync())
                        {
                            try
                            {
                                // ตรวจสอบว่ามีหมายเลขที่ถูกยกเลิกหรือไม่
                                var canceledNumber = await context.CanceledReceiptNumbers
                                    .OrderBy(c => c.CanceledDate)
                                    .FirstOrDefaultAsync();

                                if (canceledNumber != null)
                                {
                                    // ใช้หมายเลขที่ถูกยกเลิก
                                    string recycledCode = canceledNumber.ReceiptCode;
                                    context.CanceledReceiptNumbers.Remove(canceledNumber);
                                    await context.SaveChangesAsync();
                                    await transaction.CommitAsync();

                                    Debug.WriteLine($"ใช้หมายเลขรีไซเคิล: {recycledCode}");
                                    return recycledCode;
                                }

                                // ดึงหมายเลขถัดไป
                                var numberManager = await context.ReceiptNumberManagers
                                    .FirstOrDefaultAsync();

                                if (numberManager == null)
                                {
                                    // สร้างข้อมูลเริ่มต้น
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

                                // อัปเดตหมายเลขถัดไป
                                numberManager.CurrentNumber++;
                                numberManager.LastUpdated = DateTime.Now;
                                numberManager.UpdatedBy = Environment.MachineName;

                                await context.SaveChangesAsync();
                                await transaction.CommitAsync();

                                Debug.WriteLine($"สร้างหมายเลขใหม่: {nextCode}");
                                return nextCode;
                            }
                            catch
                            {
                                await transaction.RollbackAsync();
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Debug.WriteLine($"ข้อผิดพลาดในการสร้างหมายเลขใบเสร็จ (ครั้งที่ {retryCount}): {ex.Message}");

                    if (retryCount >= maxRetries)
                    {
                        throw new InvalidOperationException($"ไม่สามารถสร้างหมายเลขใบเสร็จได้หลังจากลองแล้ว {maxRetries} ครั้ง", ex);
                    }

                    // รอสักครู่ก่อนลองใหม่ (exponential backoff)
                    await Task.Delay(100 * retryCount);
                }
            }

            throw new InvalidOperationException("ไม่สามารถสร้างหมายเลขใบเสร็จได้");
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
                            CanceledDate = DateTime.Now
                        };

                        context.CanceledReceiptNumbers.Add(canceledNumber);
                        await context.SaveChangesAsync();

                        Debug.WriteLine($"เก็บหมายเลข {receiptCode} เพื่อนำมาใช้ใหม่");
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