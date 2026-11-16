import { familyAssistantApi } from '../../api/familyAssistant';
import { ChatWidget } from '../../components/ChatWidget';

const GREETING =
  'Xin chào! Tôi là trợ lý gia đình. Bạn có thể hỏi: "Hôm nay người thân tôi ăn uống thế nào?", "Huyết áp tuần này?", "Tháng này còn nợ bao nhiêu?", hoặc "Lịch thăm của tôi khi nào?".';

/** S4-H1 (AI-02): widget trợ lý AI cho tài khoản Family ở mọi trang trong AppLayout. */
export function FamilyAssistant() {
  return (
    <ChatWidget
      title="Trợ lý gia đình"
      greeting={GREETING}
      send={familyAssistantApi.chat}
      loadHistory={familyAssistantApi.history}
      disclaimer="Thông tin chỉ mang tính tra cứu từ hồ sơ đã chia sẻ, không thay thế tư vấn y tế."
    />
  );
}
