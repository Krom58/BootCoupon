using System;
using Microsoft.UI.Xaml.Data;

namespace BootCoupon
{
    public class ThaiNumberToTextConverter : IValueConverter
    {
        private static readonly string[] _digitNames = { "ศูนย์", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า" };
        private static readonly string[] _positionNames = { "", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน", "ล้าน" };

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return "ศูนย์บาทถ้วน";

            // แก้ไขบรรทัดนี้ เพื่อป้องกัน warning CS8600
            string numStr = value?.ToString() ?? string.Empty;
            numStr = numStr.Replace(",", "").Trim();
            
            if (string.IsNullOrWhiteSpace(numStr)) return "ศูนย์บาทถ้วน";

            try
            {
                // แยกส่วนจำนวนเต็มกับทศนิยม
                string[] parts = numStr.Split('.');
                decimal amount = decimal.Parse(parts[0]);
                
                if (amount == 0) return "ศูนย์บาทถ้วน";

                string result = ConvertNumberToThai(amount);
                
                // ตรวจสอบว่ามีทศนิยมหรือไม่
                if (parts.Length > 1)
                {
                    string satang = parts[1].PadRight(2, '0').Substring(0, 2);
                    int satangAmount = int.Parse(satang);
                    
                    if (satangAmount > 0)
                    {
                        result += "บาท" + ConvertNumberToThai(satangAmount) + "สตางค์";
                    }
                    else
                    {
                        result += "บาทถ้วน";
                    }
                }
                else
                {
                    result += "บาทถ้วน";
                }

                return "รวม ( " + result + " )";
            }
            catch
            {
                return "ศูนย์บาทถ้วน";
            }
        }

        private string ConvertNumberToThai(decimal number)
        {
            if (number == 0) return "ศูนย์";

            string result = "";
            int position = 0;
            
            // ตัวเลขหลักล้าน
            if (number >= 1000000)
            {
                decimal millions = Math.Floor(number / 1000000);
                result += ConvertNumberToThai(millions) + "ล้าน";
                number %= 1000000;
            }
            
            while (number > 0)
            {
                int digit = (int)(number % 10);
                number = Math.Floor(number / 10);
                
                // ข้ามถ้าเป็นเลข 0
                if (digit == 0)
                {
                    position++;
                    continue;
                }
                
                string digitName = _digitNames[digit];
                
                // กรณีพิเศษสำหรับเลขหลักสิบ
                if (position == 1)
                {
                    if (digit == 1)
                        digitName = ""; // สิบ แทน หนึ่งสิบ
                    else if (digit == 2)
                        digitName = "ยี่"; // ยี่สิบ แทน สองสิบ
                }
                
                // กรณีพิเศษสำหรับเลขหลักหน่วย
                if (position == 0 && digit == 1 && number > 0)
                    digitName = "เอ็ด"; // ยี่สิบเอ็ด แทน ยี่สิบหนึ่ง
                
                result = digitName + _positionNames[position] + result;
                position++;
            }
            
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}