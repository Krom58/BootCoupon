using System;
using System.Collections.Generic; // เพิ่มบรรทัดนี้สำหรับ List<>

using System.Diagnostics; // เพิ่มบรรทัดนี้สำหรับ Debug.WriteLine
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace BootCoupon
{
    public class AppSettings
    {
        public string ReceiptCodePrefix { get; set; } = "INV";
        public int CurrentReceiptNumber { get; set; } = 5001;

        // เพิ่มรายการหมายเลขใบเสร็จที่ถูกยกเลิก
        public List<string> CanceledReceiptNumbers { get; set; } = new List<string>();

        // ไฟล์เก็บการตั้งค่า
        private static readonly string SettingsFilePath = Path.Combine(
            ApplicationData.Current.LocalFolder.Path, "settings.json");

        // ดึงหมายเลขใบเสร็จถัดไป (ใช้หมายเลขที่ถูกยกเลิกก่อนถ้ามี)
        public string GetNextReceiptCode()
        {
            // ถ้ามีหมายเลขที่ถูกยกเลิก ใช้หมายเลขแรกในรายการ
            if (CanceledReceiptNumbers.Count > 0)
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

        // เพิ่มหมายเลขที่ถูกยกเลิกเพื่อนำมาใช้ใหม่
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
        public static async Task<string> GenerateReceiptCodeAsync()
        {
            var settings = await GetSettingsAsync();
            string receiptCode = settings.GetNextReceiptCode();
            await SaveSettingsAsync(settings);
            return receiptCode;
        }
    }
}