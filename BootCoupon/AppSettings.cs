using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;

namespace BootCoupon
{
    public class AppSettings
    {
        public string ReceiptCodePrefix { get; set; } = "INV";
        public int CurrentReceiptNumber { get; set; } = 1;
        
        // *** เพิ่ม YearCode ***
        public int YearCode { get; set; } = DateTime.Now.Year % 100;

        public List<string> CanceledReceiptNumbers { get; set; } = new List<string>();

        private static readonly string SettingsFilePath = Path.Combine(
            ApplicationData.Current.LocalFolder.Path, "settings.json");

        /// <summary>
        /// ดึงหมายเลขใบเสร็จถัดไป รูปแบบ: {Prefix}{YY}{NNN}
        /// เช่น INV25001, INV25002 (3 หลัก)
        /// </summary>
        public string GetNextReceiptCode()
        {
            // ตรวจสอบว่าขึ้นปีใหม่หรือไม่
            var currentYearCode = DateTime.Now.Year % 100;
            if (YearCode != currentYearCode)
            {
                Debug.WriteLine($"Year changed: {YearCode} → {currentYearCode} - Resetting number");
                YearCode = currentYearCode;
                CurrentReceiptNumber = 1;
            }

            // ถ้ามีหมายเลขที่ถูกยกเลิก (ของปีเดียวกัน) ใช้ก่อน
            if (CanceledReceiptNumbers.Count > 0)
            {
                var matchingYear = CanceledReceiptNumbers
                    .FirstOrDefault(code => GetYearCodeFromReceiptCode(code) == currentYearCode);
                
                if (matchingYear != null)
                {
                    CanceledReceiptNumbers.Remove(matchingYear);
                    Debug.WriteLine($"♻️ Recycled: {matchingYear}");
                    return matchingYear;
                }
            }

            // *** สร้างรหัสใหม่: Prefix + YY + NNN (3 หลัก) ***
            string nextCode = $"{ReceiptCodePrefix}{YearCode:D2}{CurrentReceiptNumber:D3}";
            return nextCode;
        }

        public void RecycleReceiptCode(string receiptCode)
        {
            if (!string.IsNullOrEmpty(receiptCode) && !CanceledReceiptNumbers.Contains(receiptCode))
            {
                CanceledReceiptNumbers.Add(receiptCode);
                Debug.WriteLine($"📝 Added to recycle list: {receiptCode}");
            }
        }

        /// <summary>
        /// ดึงปี (ค.ศ. 2 หลัก) จากรหัสใบเสร็จ
        /// เช่น INV25001 → 25
        /// </summary>
        private int GetYearCodeFromReceiptCode(string receiptCode)
        {
            if (string.IsNullOrEmpty(receiptCode) || receiptCode.Length < ReceiptCodePrefix.Length + 2)
                return 0;

            var yearPart = receiptCode.Substring(ReceiptCodePrefix.Length, 2);
            return int.TryParse(yearPart, out var year) ? year : 0;
        }

        public static async Task<AppSettings> GetSettingsAsync()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = await File.ReadAllTextAsync(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    
                    var currentYearCode = DateTime.Now.Year % 100;
                    if (settings.YearCode != currentYearCode)
                    {
                        settings.YearCode = currentYearCode;
                        settings.CurrentReceiptNumber = 1;
                        await SaveSettingsAsync(settings);
                    }
                    
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }

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

        [Obsolete("Use ReceiptNumberService.GenerateNextReceiptCodeAsync() instead")]
        public static async Task<string> GenerateReceiptCodeAsync()
        {
            return await GenerateReceiptCodeAsync(null);
        }

        [Obsolete("Use ReceiptNumberService.GenerateNextReceiptCodeAsync() instead")]
        public static async Task<string> GenerateReceiptCodeAsync(string? dbConnectionString)
        {
            Debug.WriteLine("⚠️ Warning: Using deprecated AppSettings. Use ReceiptNumberService instead.");
            
            var settings = await GetSettingsAsync();
            var code = settings.GetNextReceiptCode();
            settings.CurrentReceiptNumber++;
            await SaveSettingsAsync(settings);
            return code;
        }

        [Obsolete("Use ReceiptNumberService.RecycleReceiptCodeAsync() instead")]
        public static async Task RecycleReceiptCodeToDbAsync(string receiptCode, string? dbConnectionString = null)
        {
            var settings = await GetSettingsAsync();
            settings.RecycleReceiptCode(receiptCode);
            await SaveSettingsAsync(settings);
        }
    }
}