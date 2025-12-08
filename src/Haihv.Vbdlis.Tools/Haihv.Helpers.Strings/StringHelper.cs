using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Haihv.Tools.Hsq.Helpers;

/// <summary>
/// Helper cho các thao tác với string
/// </summary>
public static partial class StringHelper
{
    private static string[] DaMatKeywords => ["đã mất", "đã chết"];
    private static readonly string[] PrefixesNam = ["hộ ông", "ông", "hộ ông:", "ông:"];
    private static readonly string[] PrefixesNu = ["hộ bà", "bà", "hộ bà:", "bà:"];

    extension(string? text)
    {
        /// <summary>
        /// Chuẩn hóa chuỗi ngày tháng:
        /// - Nếu chuỗi rỗng hoặc null, trả về chuỗi rỗng
        /// - Nếu chuỗi dạng yyyyMMdd (8 chữ số liên tiếp), chuyển thành dd/MM/yyyy
        /// - Nếu chuỗi dạng ddMMyyyy (8 chữ số liên tiếp), chuyển thành dd/MM/yyyy
        /// - Nếu chuỗi dạng dMyyyy hoặc ddMyyyy (7 chữ số liên tiếp), chuyển thành dd/MM/yyyy
        /// - Nếu chuỗi dạng ngày tháng nhưng đã chuyển thành dạng số (ví dụ: 44197 cho ngày 01/01/2021), chuyển thành dd/MM/yyyy, nếu nằm ngoài khoảng từ 1/1/1900 đến ngày hiện tại thì thử tách theo các định dạng khác
        /// - Nếu chuỗi dạng ngày tháng với các định dạng phổ biến khác (dd/MM/yyyy, MM/dd/yyyy, dd-MM-yyyy, MM-dd-yyyy, dd.MM.yyyy, dd/MM/yy, MM/dd/yy, dd-MM-yy, MM-dd-yy, dd.MM.yy), chuyển thành dd/MM/yyyy
        /// </summary>
        /// <returns>
        /// Chuỗi ngày tháng được chuẩn hoá dạng dd/MM/yyyy (chuẩn tiếng Việt)
        /// </returns>
        public string NormalizedNgayThang()
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var raw = text.Trim();
            var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());

            // 1. Các dạng chỉ gồm số (yyyyMMdd, ddMMyyyy, dMyyyy, ddMyyyy)
            if (TryParseCompactDigits(digitsOnly, out var compactDate))
                return compactDate.ToString("dd/MM/yyyy");

            // 2. Ngày Excel (OADate) khi ô đã chuyển thành số
            if (TryParseExcelSerial(digitsOnly, out var excelDate))
                return excelDate.ToString("dd/MM/yyyy");

            // 3. Các định dạng phổ biến có dấu / - .
            var cleaned = new string(raw.Where(c => char.IsDigit(c) || c is '/' or '-' or '.').ToArray())
                .Replace('-', '/')
                .Replace('.', '/');

            if (TryParseCommonFormats(cleaned, out var formattedDate))
                return formattedDate.ToString("dd/MM/yyyy");

            // Không nhận diện được
            return string.Empty;

            bool TryParseCompactDigits(string digits, out DateTime date)
            {
                date = default;
                if (string.IsNullOrEmpty(digits))
                    return false;

                if (digits.Length == 8)
                {
                    // yyyyMMdd
                    if (int.TryParse(digits[..4], out var year) &&
                        int.TryParse(digits.AsSpan(4, 2), out var month) &&
                        int.TryParse(digits.AsSpan(6, 2), out var day) &&
                        TryCreateDate(year, month, day, out date))
                    {
                        return true;
                    }

                    // ddMMyyyy
                    if (int.TryParse(digits[..2], out var day2) &&
                        int.TryParse(digits.AsSpan(2, 2), out var month2) &&
                        int.TryParse(digits.AsSpan(4, 4), out var year2) &&
                        TryCreateDate(year2, month2, day2, out date))
                        return true;
                }

                if (digits.Length == 7)
                {
                    // dMyyyy hoặc ddMyyyy (3 chữ số đầu cho ngày + tháng, 4 chữ số cuối cho năm)
                    var yearPart = digits.Substring(3, 4);

                    // 1 chữ số ngày + 2 chữ số tháng
                    if (int.TryParse(yearPart, out var year3) &&
                        int.TryParse(digits[..1], out var day3) &&
                        int.TryParse(digits.AsSpan(1, 2), out var month3) &&
                        TryCreateDate(year3, month3, day3, out date))
                        return true;

                    // 2 chữ số ngày + 1 chữ số tháng
                    if (int.TryParse(yearPart, out var year4) &&
                        int.TryParse(digits[..2], out var day4) &&
                        int.TryParse(digits.AsSpan(2, 1), out var month4) &&
                        TryCreateDate(year4, month4, day4, out date))
                        return true;
                }

                return false;
            }

            bool TryParseExcelSerial(string digits, out DateTime date)
            {
                date = default;
                if (string.IsNullOrEmpty(digits) || !double.TryParse(digits, out var serial))
                    return false;

                // Excel/OA date: bắt đầu từ 30/12/1899, chấp nhận từ 1/1/1900 đến hiện tại
                var minSerial = new DateTime(1900, 1, 1).ToOADate();
                var maxSerial = DateTime.Now.ToOADate();

                if (serial < minSerial || serial > maxSerial)
                    return false;

                date = DateTime.FromOADate(serial);
                return true;
            }

            bool TryParseCommonFormats(string value, out DateTime date)
            {
                date = default;
                if (string.IsNullOrWhiteSpace(value))
                    return false;

                string[] formats =
                [
                    "dd/MM/yyyy", "MM/dd/yyyy", "dd-MM-yyyy", "MM-dd-yyyy", "dd.MM.yyyy",
                    "dd/MM/yy", "MM/dd/yy", "dd-MM-yy", "MM-dd-yy", "dd.MM.yy",
                    "d/M/yyyy", "M/d/yyyy", "d-M-yyyy", "M-d-yyyy", "d.M.yyyy"
                ];

                return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out date);
            }

            bool TryCreateDate(int year, int month, int day, out DateTime date)
            {
                date = default;
                if (!IsValidYear(year) || month is < 1 or > 12)
                    return false;

                var maxDay = DateTime.DaysInMonth(year, month);
                if (day is < 1 || day > maxDay)
                    return false;

                date = new DateTime(year, month, day);
                return true;
            }
        }

        /// <summary>
        /// Chuẩn hóa chuỗi: bỏ dấu tiếng Việt, chuyển thành chữ thường, bỏ khoảng trắng và ký tự đặc biệt
        /// </summary>
        public string NormalizedNameWithoutSpaces()
            => text.NormalizedFileName();

        /// <summary>
        /// Chuẩn hoá tên người tiếng Việt
        /// Chuẩn hoá viết hoa đầu chữ
        /// Loại bỏ hộ ông, hộ bà, ông, bà ở đầu không phân biệt chữ hoa, chữ thường
        /// </summary>
        /// <returns>
        /// Tên được chuẩn hoá
        /// </returns>
        public string NormalizedNameVietnamese()
        {
            {
                if (string.IsNullOrWhiteSpace(text))
                    return string.Empty;

                // Loại bỏ khoảng trắng thừa
                var name = SpaceRegex().Replace(text.Trim(), " ");

                // Loại bỏ các tiền tố: hộ ông, hộ bà, ông, bà (không phân biệt hoa thường)
                string[] prefixes = [.. PrefixesNam, .. PrefixesNu];

                foreach (var prefix in prefixes)
                {
                    if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    name = name[prefix.Length..].Trim();
                    break;
                }

                // Chuẩn hóa viết hoa đầu chữ cho từng từ
                var textInfo = new CultureInfo("vi-VN", false).TextInfo;
                name = textInfo.ToTitleCase(name.ToLower());

                return name;
            }
        }

        public string GetGioTinhFromName()
        {
            {
                if (string.IsNullOrWhiteSpace(text))
                    return string.Empty;

                // Loại bỏ khoảng trắng thừa
                var name = SpaceRegex().Replace(text.Trim(), " ");

                // Kiểm tra các tiền tố: hộ ông, hộ bà, ông, bà (không phân biệt hoa thường)
                foreach (var prefix in PrefixesNam)
                {
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return "Nam";
                }

                foreach (var prefix in PrefixesNu)
                {
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return "Nữ";
                }

                return string.Empty;
            }
        }

        public string CheckDaMatKeywords()
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var group2Lower = text.ToLowerInvariant();
            // nếu group2 chứa một trong các từ trong DaMatKeywords thì gán empty
            return DaMatKeywords.Any(k => group2Lower.Contains(k, StringComparison.InvariantCultureIgnoreCase))
                ? string.Empty
                : text;
        }


        /// <summary>
        /// Trích xuất tên thành 2 phần (text1, text2) dựa trên các ký tự phân tách phổ biến:
        /// Sử dụng để tách 2 tên hoặc 2 số giấy tờ trên 1 ô Excel.
        /// Ký tự phân tách theo thứ tự ưu tiên:
        /// - Xuống dòng (\r\n hoặc \n)
        /// - Ngoặc đơn ()
        /// - Dấu gạch ngang (-)
        /// - Dấu chấm phẩy (;)
        /// - Dấu phẩy (,)
        /// Nếu không tìm thấy ký tự phân tách nào, trả về toàn bộ chuỗi ở ten1, ten2 là chuỗi rỗng.
        /// </summary>
        /// <returns>
        /// (text1, text2)
        /// </returns>
        public (string text1, string text2)? ExtractText1AndText2()
        {
            if (string.IsNullOrEmpty(text))
                return (string.Empty, string.Empty);

            text = text.Trim().ToLowerInvariant();
            var part1 = text;
            var part2 = string.Empty;

            // 1. Tách theo xuống dòng (Enter trong Excel: \r\n hoặc \n)
            if (text.Contains("\r\n") || text.Contains('\n'))
            {
                var parts = text.Split(["\r\n", "\n"], 2, StringSplitOptions.None);
                part1 = parts[0].Trim();
                part2 = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                return (part1, part2.CheckDaMatKeywords());
            }

            // 2. Tách theo nội dung trong ngoặc đơn ()
            var matchParentheses = ExtractNameInParenthesesRegex().Match(text);
            if (matchParentheses.Success)
            {
                part1 = matchParentheses.Groups[1].Value.Trim();
                part2 = matchParentheses.Groups[2].Value.Trim();
                return (part1, part2.CheckDaMatKeywords());
            }

            // 3. Tách theo dấu - (gạch ngang)
            if (text.Contains('-'))
            {
                var parts = text.Split('-', 2);
                part1 = parts[0].Trim();
                part2 = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                return (part1, part2.CheckDaMatKeywords());
            }

            // 4. Tách theo dấu ; (chấm phẩy)
            if (text.Contains(';'))
            {
                var parts = text.Split(';', 2);
                part1 = parts[0].Trim();
                part2 = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                return (part1, part2);
            }

            // 5. Tách theo dấu , (phẩy)
            if (text.Contains(','))
            {
                var parts = text.Split(',', 2);
                part1 = parts[0].Trim();
                part2 = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                return (part1, part2);
            }

            // 6. Tách theo chuỗi "và" (và)
            if (!text.Contains("và")) return (part1, part2);
            {
                var parts = text.Split("và", 2, StringSplitOptions.RemoveEmptyEntries);
                part1 = parts[0].Trim();
                part2 = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                return (part1, part2);
            }
            // Nếu không có ký tự phân tách nào, trả về toàn bộ chuỗi ở part1
        }

        /// <summary>
        /// Tách năm sinh từ ngày tháng năm sinh dạng text trên Excel hoặc ngày tháng trên Excel
        /// Không phân biệt:
        /// yyyyMMdd
        /// dd/MM/yyyy
        /// MM/dd/yyyy
        /// dd-MM-yyyy
        /// MM-dd-yyyy
        /// </summary>
        /// <returns>
        /// Năm sinh (4 chữ số) hoặc null nếu không tìm thấy
        /// </returns>
        public int? ExtractYearOfBirth()
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Loại bỏ khoảng trắng thừa
            text = text.Trim();

            // Kiểm tra định dạng yyyyMMdd (8 chữ số liên tiếp)
            var yyyyMMddMatch = CompactDatePattern().Match(text);
            if (yyyyMMddMatch.Success)
            {
                var dateStr = yyyyMMddMatch.Groups[1].Value;
                // Lấy 4 chữ số đầu là năm
                if (int.TryParse(dateStr.AsSpan(0, 4), out var yearFromCompact))
                {
                    // Kiểm tra năm hợp lệ (từ 1900 đến năm hiện tại)
                    if (IsValidYear(yearFromCompact))
                    {
                        return yearFromCompact;
                    }
                }
            }

            // Tách các phần theo dấu phân tách phổ biến
            var parts = text.Split(['/', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 3)
            {
                // Tìm phần có độ dài 4 ký tự và là số
                foreach (var part in parts)
                {
                    if (part.Length != 4 || !int.TryParse(part, out var year)) continue;
                    if (IsValidYear(year))
                    {
                        return year;
                    }
                }
            }

            // Tìm kiếm năm 4 chữ số bất kỳ trong chuỗi
            var yearMatch = YearPattern().Match(text);
            if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var foundYear))
            {
                return foundYear;
            }

            return null;
        }

        /// <summary>
        /// Chuẩn hoá số định danh cá nhân - Số căn cước - Căn cước công dân
        /// Text đầu vào chuẩn hoá có ít nhất 10 ký tự và tối đa 12 ký tự
        /// Lớn hơn 12 ký tự: Không thực hiện
        /// 9 ký tự là số Chứng minh nhân dân: Không thực hiện
        /// </summary>
        /// <returns>
        /// Chuỗi chuẩn hoá gồm 12 ký tự thêm số 0 ở đầu nếu thiếu
        /// Hoặc bỏ qua nếu lớn hơn 12 ký tự hoặc nhỏ hơn 10 ký tự
        /// </returns>
        public string? NormalizePersonalId()
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Loại bỏ các ký tự không phải số
            var digitsOnly = new string([.. text.Where(char.IsDigit)]);

            return digitsOnly.Length is > 12 or < 10
                ?
                // Không thực hiện nếu lớn hơn 12 ký tự hoặc nhỏ hơn 10 ký tự
                null
                :
                // Thêm số 0 ở đầu nếu thiếu để đủ 12 ký tự
                digitsOnly.PadLeft(12, '0');
        }

        /// <summary>
        /// Tách giới tính và năm sinh từ số định danh cá nhân - Số căn cước - Căn cước công dân đã chuẩn hoá
        /// Tách 3 chữ số sau mã tỉnh: từ số thứ 4 đến số thứ 6 (0-based index 3 đến 5)
        /// Chữ số đầu tiên là giới tính:
        /// 0: Nam (sinh từ 1900 - 1999)
        /// 1: Nữ (sinh từ 1900 - 1999)
        /// 2: Nam (sinh từ 2000 - 2099)
        /// 3: Nữ (sinh từ 2000 - 2099)
        /// 2 chữ số tiếp theo là năm sinh
        /// </summary>
        /// <returns>
        /// (GioiTinh, NamSinh) hoặc null nếu không thể tách được
        /// GioiTinh: "Nam" hoặc "Nữ"
        /// NamSinh: Năm sinh (4 chữ số)
        /// </returns>
        public (string? GioiTinh, int? NamSinh)? ParsePersonalId()
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var gioiTinh = text[3];
            var namSinh = int.Parse(text.Substring(4, 2));

            // Thêm tiền tố thế kỷ: 1900-1999 => 19, 2000-2099 => 20
            namSinh += gioiTinh is '0' or '1' ? 1900 : 2000;

            return gioiTinh is '0' or '2' ? ("Nam", namSinh) : ("Nữ", namSinh);
        }
        public string NormalizeGioiTinh(string? soDinhDanh = null, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                // Thử lấy từ số định danh
                var personalInfo = soDinhDanh?.NormalizePersonalId()?.ParsePersonalId();
                if (personalInfo?.GioiTinh != null)
                    return personalInfo.Value.GioiTinh;

                // Thử lấy từ tên
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name.GetGioTinhFromName();
                }
            }
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = text.Trim().ToLowerInvariant();

            return normalized.StartsWith("nu") || normalized.StartsWith("nữ")
                ? "Nữ"
                : normalized.StartsWith("nam")
                    ? "Nam"
                    : string.Empty;
        }
        /// <summary>
        /// Chuẩn hóa chuỗi: bỏ dấu tiếng Việt, chuyển thành chữ thường, bỏ khoảng trắng và ký tự đặc biệt
        /// Ví dụ:
        /// - "Giấy Chứng Nhận" => "giaychungnhan"
        /// - "Giay_chung_nhan" => "giaychungnhan"
        /// - "Giay Chung Nhan" => "giaychungnhan"
        /// - "GCN (mới)" => "gcnmoi"
        /// </summary>
        private string NormalizedFileName()
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Chuyển về chữ thường
            text = text.ToLowerInvariant();

            // Phân tách ký tự thành dạng cơ bản và dấu (FormD)
            var normalized = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            // Duyệt qua từng ký tự, loại bỏ các ký tự dấu (NonSpacingMark)
            foreach (var c in from c in normalized
                              let category = CharUnicodeInfo.GetUnicodeCategory(c)
                              where category != UnicodeCategory.NonSpacingMark
                              select c)
            {
                stringBuilder.Append(c);
            }

            // Bỏ khoảng trắng, dấu gạch dưới và các ký tự đặc biệt
            // Chỉ giữ chữ cái (a-z) và số (0-9)
            var result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            // Lưu ý: \w bao gồm cả dấu gạch dưới (_), nên ta phải loại bỏ riêng
            return result.Replace("_", "").Replace(".", "").Replace("-", "").Replace(" ", "");
        }

        /// <summary>
        /// Chuẩn hóa số phát hành của Giấy chứng nhận.
        /// </summary>
        /// <returns>
        /// Chuỗi chuẩn:
        /// "A 123456"
        /// "AB 123456"
        /// "AB 12345678"
        /// </returns>
        public string NormalizedSoPhatHanh()
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // 1. Loại bỏ các ký tự đặc biệt phổ biến
            var cleaned = text.Trim()
                .Replace("_", "")
                .Replace(".", "")
                .Replace("-", "")
                .Replace(" ", "");

            // 2. Tìm phần chữ + phần số đúng chuẩn (1–2 chữ + 6–8 số)
            var match = SoPhatHanhRegex2().Match(cleaned);

            if (!match.Success)
                return cleaned; // hoặc return ""; tùy bạn muốn

            var letters = match.Groups["letters"].Value.ToUpper();
            var digits = match.Groups["digits"].Value;

            // 3. Chuẩn hóa đúng 1 dấu cách
            return $"{letters} {digits}";
        }


        /// <summary>
        /// Tìm thứ tự ưu tiên của từ khóa GCN trong tên file
        /// Trả về index của từ khóa đầu tiên tìm thấy (0 = ưu tiên cao nhất)
        /// Trả về -1 nếu không tìm thấy (không phải GCN)
        /// Trả về int.MaxValue - 1 nếu chứa từ khóa loại trừ trong () - KHÔNG phải GCN
        /// Trả về int.MaxValue - 2 nếu chứa từ khóa ưu tiên thấp (không có ngoặc) - vẫn là GCN nhưng ưu tiên thấp
        /// </summary>
        public int FindCertificatePriority(string[] certificateNames, string? soPhatHanh = null,
            string[]? excludeKeywords = null, string[]? lowPriorityKeywords = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception("Tên file không được rỗng khi tìm thứ tự ưu tiên GCN.");
            var normalizedFileName = text.NormalizedFileName();
            var fileNameLowerNoSpace =
                text.ToLowerInvariant().Replace(" ", ""); // Loại bỏ khoảng trắng để so sánh excludeKeywords

            // 1. Kiểm tra từ khóa loại trừ (trong ngoặc đơn) - Loại bỏ khoảng trắng, so sánh chữ thường
            if (excludeKeywords != null && excludeKeywords.Length > 0)
            {
                foreach (var keyword in excludeKeywords)
                {
                    if (fileNameLowerNoSpace.Contains(keyword, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return -1; // KHÔNG phải GCN
                    }
                }
            }

            // 2. Kiểm tra số phát hành (ưu tiên cao nhất)
            if (!string.IsNullOrEmpty(soPhatHanh))
            {
                var normalizedSoPhatHanh = soPhatHanh.NormalizedFileName();
                if (normalizedFileName.Contains(normalizedSoPhatHanh))
                {
                    // Kiểm tra từ khóa ưu tiên thấp
                    if (HasLowPriorityKeywords(normalizedFileName, lowPriorityKeywords))
                    {
                        return int.MaxValue - 2; // Ưu tiên thấp
                    }

                    return 0; // Ưu tiên cao nhất nếu tìm thấy số phát hành
                }
            }

            // 3. Kiểm tra tên GCN
            for (var i = 0; i < certificateNames.Length; i++)
            {
                var normalizedCertName = certificateNames[i].NormalizedFileName();
                if (normalizedFileName.Contains(normalizedCertName))
                {
                    // Kiểm tra từ khóa ưu tiên thấp
                    if (HasLowPriorityKeywords(normalizedFileName, lowPriorityKeywords))
                    {
                        return int.MaxValue - 2; // Ưu tiên thấp
                    }

                    return i; // Trả về index = thứ tự ưu tiên
                }
            }

            // Không tìm thấy
            return -1; // Không phải GCN
        }

        /// <summary>
        /// Kiểm tra xem file có chứa từ khóa ưu tiên thấp không
        /// </summary>
        private static bool HasLowPriorityKeywords(string normalizedFileName, string[]? lowPriorityKeywords)
        {
            if (lowPriorityKeywords == null || lowPriorityKeywords.Length == 0)
                return false;

            return lowPriorityKeywords.Any(normalizedFileName.Contains);
        }
    }

    /// <summary>
    /// Kiểm tra năm có hợp lệ không (từ 1900 đến năm hiện tại)
    /// </summary>
    private static bool IsValidYear(int year)
    {
        return year >= 1900 && year <= DateTime.Now.Year;
    }

    /// <summary>
    /// Pattern: Tìm năm sinh 4 chữ số (1900-2099)
    /// </summary>
    [GeneratedRegex(@"\b(19\d{2}|20\d{2})\b")]
    private static partial Regex YearPattern();

    /// <summary>
    /// Pattern: Tìm ngày tháng năm định dạng yyyyMMdd (8 chữ số liên tiếp)
    /// </summary>
    [GeneratedRegex(@"\b(\d{8})\b")]
    private static partial Regex CompactDatePattern();


    // Pattern: Bắt đầu với số tờ + dấu cách/dấu gạch dưới/dấu chấm/dấu gạch ngang + số thửa
    // Ví dụ: "5 123", "12-456", "3_789", "7.890"
    // Chỉ khớp nếu nằm ở đầu chuỗi
    [GeneratedRegex(@"^(\d+)[\s._-](\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SoToSoThuaRegex();

    // Pattern: Tìm cặp "số tờ + số thửa" ở bất kỳ vị trí nào trong chuỗi
    // Ví dụ khớp: "5 123", "12-456", "3_789", "7.890", "abc 5 123 xyz", "GCN-7.890"
    // Không yêu cầu nằm ở đầu chuỗi
    [GeneratedRegex(@"(\d+)[\s._-](\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SoToSoThuaAllowAnywhereRegex();

    // Pattern: 1 hoặc 2 chữ cái (không phân biệt hoa thường) + (có hoặc không có dấu cách) + 6–8 chữ số
    // Có thể nằm ở bất kỳ vị trí nào trong chuỗi
    [GeneratedRegex(@"\p{L}{1,2}\s?\d{6,8}\b", RegexOptions.IgnoreCase)]
    private static partial Regex SoPhatHanhRegex();

    [GeneratedRegex(@"^(?<letters>\p{L}{1,2})(?<digits>\d{6,8})$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SoPhatHanhRegex2();

    [GeneratedRegex(@"^(.*?)\s*\((.*?)\)\s*$")]
    private static partial Regex ExtractNameInParenthesesRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    extension(string fileName)
    {
        /// <summary>
        /// Trích xuất số phát hành từ tên file.
        /// Có 1 hoặc 2 chữ cái (không phân biệt hoa thường)
        /// + (có hoặc không có dấu cách)
        /// + 6–8 chữ số.
        /// Có thể nằm ở bất kỳ vị trí nào trong chuỗi.
        /// </summary>
        public string? ExtractSoPhatHanh()
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // Chuẩn hóa tên file, không sử dụng NormalizedFileName vì cần giữ lại khoảng trắng
            var match = SoPhatHanhRegex().Match(fileName.Trim().Normalize(NormalizationForm.FormC));
            return match.Success ? match.Groups[0].Value : null;
        }

        /// <summary>
        /// Trích xuất số tờ và số thửa từ tên file
        /// Ví dụ: "5 123 GCN.pdf" => (5, 123)
        /// Chỉ lấy nếu file bắt đầu bằng pattern "số tờ" + khoảng trắng + "số thửa"
        /// </summary>
        public (int soTo, int soThua)? ExtractSoToSoThua(bool allowAnywhere = false)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // Chuẩn hóa tên file, không sử dụng NormalizedFileName vì cần giữ lại khoảng trắng
            fileName = fileName.Trim();

            if (fileName.Length < 3) // Tối thiểu phải có "1 1"
                return null;

            if (allowAnywhere)
            {
                // Pattern: Ở bất kỳ vị trí nào trong chuỗi
                var match = SoToSoThuaAllowAnywhereRegex().Match(fileName);
                if (match.Success &&
                    int.TryParse(match.Groups[1].Value, out var soTo) &&
                    int.TryParse(match.Groups[2].Value, out var soThua))
                {
                    return (soTo, soThua);
                }
            }
            else
            {
                // Pattern: Bắt đầu file với số tờ + khoảng trắng/dấu gạch + số thửa
                var match = SoToSoThuaRegex().Match(fileName);
                if (match.Success &&
                    int.TryParse(match.Groups[1].Value, out var soTo) &&
                    int.TryParse(match.Groups[2].Value, out var soThua))
                {
                    return (soTo, soThua);
                }
            }

            return null;
        }

        /// <summary>
        /// Kiểm tra xem tên file có chứa từ khóa không mong muốn (cũ, thu hồi, trước, th)
        /// Không phân biệt hoa thường, dấu cách, _
        /// </summary>
        public bool HasUnwantedKeywords(string[]? unwantedKeywords)
        {
            if (unwantedKeywords == null || unwantedKeywords.Length == 0)
                return false;

            var normalized = fileName.NormalizedFileName();
            return unwantedKeywords.Any(keyword => normalized.Contains(keyword.NormalizedFileName()));
        }
    }

    /// <summary>
    /// Viết hoa chữ cái đầu của mỗi từ (Title Case)
    /// Ví dụ: "phường kinh bắc" => "Phường Kinh Bắc"
    /// </summary>
    public static string ToTitleCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var textInfo = CultureInfo.CurrentCulture.TextInfo;

        // ToTitleCase của .NET không hoạt động tốt với từ viết hoa toàn bộ
        // Nên convert về lowercase trước
        var lowerCase = input.ToLower();

        return textInfo.ToTitleCase(lowerCase);
    }
}