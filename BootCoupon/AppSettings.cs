using System;
using System.Collections.Generic; // เพิ่มบรรทัดนี้สำหรับ List<>

using System.Diagnostics; // เพิ่มบรรทัดนี้สำหรับ Debug.WriteLine
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using System.Data;
using Microsoft.Data.SqlClient;

namespace BootCoupon
{
    public class AppSettings
    {
        public string ReceiptCodePrefix { get; set; } = "INV";
        public int CurrentReceiptNumber { get; set; } =5001;

        // เพิ่มรายการหมายเลขใบเสร็จที่ถูกยกเลิก
        public List<string> CanceledReceiptNumbers { get; set; } = new List<string>();

        // ไฟล์เก็บการตั้งค่า
        private static readonly string SettingsFilePath = Path.Combine(
            ApplicationData.Current.LocalFolder.Path, "settings.json");

        // ดึงหมายเลขใบเสร็จถัดไป (ใช้หมายเลขที่ถูกยกเลิกก่อนถ้ามี)
        public string GetNextReceiptCode()
        {
            // ถ้ามีหมายเลขที่ถูกยกเลิก ใช้หมายเลขแรกในรายการ
            if (CanceledReceiptNumbers.Count >0)
            {
                string recycledCode = CanceledReceiptNumbers[0];
                CanceledReceiptNumbers.RemoveAt(0);
                return recycledCode;
            }

            // ถ้าไม่มี ใช้หมายเลขถัดไป
            string nextCode = $"{ReceiptCodePrefix}{CurrentReceiptNumber}";
            CurrentReceiptNumber++;

            return nextCode;
        }

        // เพิ่มหมายเลขที่ถูกยกเลิกเพื่อนำมาใช้ใหม่ (local)
        public void RecycleReceiptCode(string receiptCode)
        {
            if (!string.IsNullOrEmpty(receiptCode) && !CanceledReceiptNumbers.Contains(receiptCode))
            {
                CanceledReceiptNumbers.Add(receiptCode);
            }
        }

        // โหลดการตั้งค่าจากไฟล์
        public static async Task<AppSettings> GetSettingsAsync()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = await File.ReadAllTextAsync(SettingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }

        // บันทึกการตั้งค่าลงไฟล์
        public static async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        // เพิ่มเมธอดสแตติกเพื่อสร้างรหัสใบเสร็จ
        // เวอร์ชันเดิมเพื่อความเข้ากันได้
        public static async Task<string> GenerateReceiptCodeAsync()
        {
            return await GenerateReceiptCodeAsync(null);
        }

        // เวอร์ชันใหม่: พยายามใช้ shared DB ก่อน (ถ้ามี connection string)
        // หากไม่สามารถใช้ DB ได้ จะ fallback เป็น local file-based generator
        public static async Task<string> GenerateReceiptCodeAsync(string? dbConnectionString)
        {
            // พยายามใช้ DB ถ้ามี connection string
            if (!string.IsNullOrWhiteSpace(dbConnectionString))
            {
                try
                {
                    var settings = await GetSettingsAsync();
                    string prefix = settings.ReceiptCodePrefix ?? "INV";

                    await using var conn = new SqlConnection(dbConnectionString);
                    await conn.OpenAsync();

                    await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable);

                    //1) พยายามดึงหมายเลขที่รีไซเคิล (pop)
                    using (var pick = new SqlCommand(
                        "SELECT TOP (1) Number FROM dbo.CanceledReceiptNumbers WITH (UPDLOCK, READPAST) ORDER BY Id",
                        conn, (SqlTransaction)tx))
                    {
                        var canceledObj = await pick.ExecuteScalarAsync();
                        if (canceledObj != null && canceledObj != DBNull.Value)
                        {
                            string canceledNumber = canceledObj.ToString() ?? string.Empty;

                            using var del = new SqlCommand("DELETE FROM dbo.CanceledReceiptNumbers WHERE Number = @num", conn, (SqlTransaction)tx);
                            del.Parameters.AddWithValue("@num", canceledNumber);
                            await del.ExecuteNonQueryAsync();

                            await tx.CommitAsync();
                            return canceledNumber;
                        }
                    }

                    //2) ถ้าไม่มี รีบใช้ SEQUENCE (ต้องสร้าง SEQUENCE ใน DB ก่อน)
                    using (var seq = new SqlCommand("SELECT NEXT VALUE FOR dbo.ReceiptNumbers", conn, (SqlTransaction)tx))
                    {
                        var nextObj = await seq.ExecuteScalarAsync();
                        long nextVal = Convert.ToInt64(nextObj);
                        await tx.CommitAsync();
                        return $"{prefix}{nextVal}";
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DB receipt generation failed: {ex.Message} -- falling back to local");
                    // fallthrough -> local fallback
                }
            }

            // Fallback: local-only generator (เดิม)
            var localSettings = await GetSettingsAsync();
            string localCode = localSettings.GetNextReceiptCode();
            await SaveSettingsAsync(localSettings);
            return localCode;
        }

        // ส่งหมายเลขที่ยกเลิกไปเก็บยัง DB (ถ้ามี connection string)
        // ถ้าไม่สำเร็จ ให้เก็บ local
        public static async Task RecycleReceiptCodeToDbAsync(string receiptCode, string? dbConnectionString = null)
        {
            if (string.IsNullOrWhiteSpace(receiptCode)) return;

            if (!string.IsNullOrWhiteSpace(dbConnectionString))
            {
                try
                {
                    await using var conn = new SqlConnection(dbConnectionString);
                    await conn.OpenAsync();
                    await using var cmd = new SqlCommand("INSERT INTO dbo.CanceledReceiptNumbers (Number) VALUES (@num)", conn);
                    cmd.Parameters.AddWithValue("@num", receiptCode);
                    await cmd.ExecuteNonQueryAsync();
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Recycle to DB failed: {ex.Message} -- storing locally instead");
                }
            }

            // ถ้าไม่สามารถส่งไป DB ได้ ให้เก็บไว้ local (จะถูกใช้เมื่อต่อ DB ไม่ได้)
            var settings = await GetSettingsAsync();
            settings.RecycleReceiptCode(receiptCode);
            await SaveSettingsAsync(settings);
        }
    }
}