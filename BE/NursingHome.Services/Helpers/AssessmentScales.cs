using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;

namespace NursingHome.Services.Helpers;

/// <summary>
/// S3-H5: thang điểm sàng lọc chuẩn hóa (Morse, Barthel, MNA-SF). Điểm luôn tính ở server.
/// Công cụ sàng lọc, không thay thế chẩn đoán.
/// </summary>
public static class AssessmentScales
{
    private static readonly IReadOnlyList<AssessmentScaleDto> AllScales =
    [
        new()
        {
            Type = nameof(AssessmentType.FallRisk),
            Title = "Đánh giá nguy cơ té ngã (Morse)",
            MaxScore = 125,
            Questions =
            [
                Q("history", "1. Tiền sử té ngã (trong 3 tháng qua)", ("no", "Không", 0), ("yes", "Có", 25)),
                Q("secondary", "2. Có từ 2 bệnh lý chẩn đoán trở lên", ("no", "Không", 0), ("yes", "Có", 15)),
                Q("aid", "3. Dụng cụ hỗ trợ di chuyển", ("none", "Không dùng / nằm tại giường / nhân viên đỡ", 0), ("crutch", "Nạng / gậy / khung tập đi", 15), ("furniture", "Bám vào bàn ghế / tường", 30)),
                Q("iv", "4. Đang truyền dịch / ống thông", ("no", "Không", 0), ("yes", "Có", 20)),
                Q("gait", "5. Dáng đi / khả năng di chuyển", ("normal", "Bình thường / nằm yên / xe lăn", 0), ("weak", "Yếu", 10), ("impaired", "Mất thăng bằng / loạng choạng", 20)),
                Q("mental", "6. Nhận thức về khả năng bản thân", ("oriented", "Tự đánh giá đúng khả năng", 0), ("forgets", "Quên hạn chế của bản thân", 15))
            ]
        },
        new()
        {
            Type = nameof(AssessmentType.Barthel),
            Title = "Đánh giá mức độ độc lập sinh hoạt (Barthel ADL)",
            MaxScore = 100,
            Questions =
            [
                Q("feeding", "1. Ăn uống", ("0", "Phụ thuộc hoàn toàn", 0), ("5", "Cần hỗ trợ cắt/xúc", 5), ("10", "Tự ăn được", 10)),
                Q("bathing", "2. Tắm rửa", ("0", "Phụ thuộc", 0), ("5", "Tự tắm được", 5)),
                Q("grooming", "3. Chăm sóc cá nhân (rửa mặt, chải đầu, đánh răng)", ("0", "Cần giúp đỡ", 0), ("5", "Tự làm", 5)),
                Q("dressing", "4. Mặc quần áo", ("0", "Phụ thuộc", 0), ("5", "Cần hỗ trợ một phần", 5), ("10", "Tự mặc", 10)),
                Q("bowels", "5. Kiểm soát đại tiện", ("0", "Không tự chủ", 0), ("5", "Thỉnh thoảng mất kiểm soát", 5), ("10", "Tự chủ hoàn toàn", 10)),
                Q("bladder", "6. Kiểm soát tiểu tiện", ("0", "Không tự chủ / đặt sonde", 0), ("5", "Thỉnh thoảng mất kiểm soát", 5), ("10", "Tự chủ hoàn toàn", 10)),
                Q("toilet", "7. Đi vệ sinh", ("0", "Phụ thuộc", 0), ("5", "Cần hỗ trợ", 5), ("10", "Tự làm", 10)),
                Q("transfer", "8. Di chuyển giường ↔ ghế", ("0", "Không thể tự ngồi", 0), ("5", "Cần hỗ trợ nhiều", 5), ("10", "Cần hỗ trợ ít", 10), ("15", "Tự làm", 15)),
                Q("mobility", "9. Đi lại trên mặt phẳng", ("0", "Bất động", 0), ("5", "Tự đẩy xe lăn", 5), ("10", "Đi với người hỗ trợ", 10), ("15", "Tự đi ≥ 50m", 15)),
                Q("stairs", "10. Lên xuống cầu thang", ("0", "Không thể", 0), ("5", "Cần hỗ trợ", 5), ("10", "Tự đi được", 10))
            ]
        },
        new()
        {
            Type = nameof(AssessmentType.Nutrition),
            Title = "Sàng lọc dinh dưỡng (MNA-SF rút gọn)",
            MaxScore = 14,
            Questions =
            [
                Q("appetite", "1. Giảm lượng ăn trong 3 tháng qua", ("0", "Giảm nhiều", 0), ("1", "Giảm vừa", 1), ("2", "Không giảm", 2)),
                Q("weight", "2. Sụt cân trong 3 tháng qua", ("0", "Sụt > 3 kg", 0), ("1", "Không rõ", 1), ("2", "Sụt 1–3 kg", 2), ("3", "Không sụt cân", 3)),
                Q("mobility", "3. Khả năng vận động", ("0", "Nằm tại giường / ghế", 0), ("1", "Ra khỏi giường nhưng không ra ngoài", 1), ("2", "Đi lại bình thường", 2)),
                Q("stress", "4. Bệnh cấp tính hoặc stress tâm lý 3 tháng qua", ("0", "Có", 0), ("2", "Không", 2)),
                Q("neuro", "5. Vấn đề tâm thần kinh (sa sút trí tuệ / trầm cảm)", ("0", "Nặng", 0), ("1", "Nhẹ", 1), ("2", "Không có", 2)),
                Q("bmi", "6. Chỉ số BMI / thể trạng", ("0", "BMI < 19 (gầy nhiều)", 0), ("1", "BMI 19–21", 1), ("2", "BMI 21–23", 2), ("3", "BMI ≥ 23", 3))
            ]
        }
    ];

    public static IReadOnlyList<AssessmentScaleDto> GetScales() => AllScales;

    public static (int Score, int MaxScore, RiskLevel Risk) Score(AssessmentType type, IReadOnlyDictionary<string, string> answers)
    {
        var scale = AllScales.First(s => s.Type == type.ToString());
        int total = 0;

        foreach (var q in scale.Questions)
        {
            if (!answers.TryGetValue(q.Key, out string? selectedKey) || string.IsNullOrWhiteSpace(selectedKey))
            {
                throw new BusinessRuleException($"Vui lòng trả lời đầy đủ mục \"{q.Label}\".");
            }

            var option = q.Options.FirstOrDefault(o => o.Key == selectedKey)
                ?? throw new BusinessRuleException($"Lựa chọn \"{selectedKey}\" không hợp lệ cho mục \"{q.Label}\".");
            total += option.Points;
        }

        var risk = type switch
        {
            AssessmentType.FallRisk => total >= 45 ? RiskLevel.High : total >= 25 ? RiskLevel.Medium : RiskLevel.Low,
            AssessmentType.Barthel => total <= 60 ? RiskLevel.High : total <= 90 ? RiskLevel.Medium : RiskLevel.Low,
            AssessmentType.Nutrition => total <= 7 ? RiskLevel.High : total <= 11 ? RiskLevel.Medium : RiskLevel.Low,
            _ => RiskLevel.Low
        };

        return (total, scale.MaxScore, risk);
    }

    private static AssessmentQuestionDto Q(string key, string label, params (string Key, string Label, int Points)[] options) =>
        new()
        {
            Key = key,
            Label = label,
            Options = options.Select(o => new AssessmentOptionDto { Key = o.Key, Label = o.Label, Points = o.Points }).ToList()
        };
}
