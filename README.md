# Fresher/Junior AI Automation & Application Engineer

## Thông tin công việc và bài đánh giá năng lực đầu vào

Tài liệu này giúp ứng viên hiểu bài toán thực tế và phạm vi công việc của vị trí **Fresher/Junior AI Automation & Application Engineer**. Công việc tập trung vào phát triển ứng dụng và tích hợp AI để tự động hóa các quy trình của công ty. Các công việc cụ thể sẽ có người hướng dẫn trong quá trình thực hiện.

## 1\. Bài đánh giá năng lực đầu vào: Tự động tạo báo giá

### 1.1. Bối cảnh và mục tiêu

Công ty sử dụng hệ thống quản lý nội bộ được phát triển bằng **Laravel**. Từ hệ thống này, người dùng tạo yêu cầu báo giá với thông tin sản phẩm và giá tương ứng, chẳng hạn sản phẩm A có giá 123 VND, sản phẩm B có giá 345 VND.

Mục tiêu của bài thực hành là để AI tiếp nhận thông tin trong yêu cầu, điền vào đúng vị trí trên mẫu **PowerPoint báo giá** có sẵn và xuất ra file báo giá đã được điền thông tin.

### 1.2. Cách nhận diện các vị trí cần điền

Trong mẫu PowerPoint, các vị trí cần điền thông tin được đánh dấu bằng `[]`. Các đối tượng liên quan cũng được đặt tên theo dạng `txt_*` trong **Selection Pane** để hỗ trợ AI nhận diện và phân biệt các trường thông tin.

Ứng viên cần đối chiếu mẫu PowerPoint với ảnh chụp màn hình phần Selection Pane để hiểu vị trí và tên của các trường cần xử lý.

### 1.3. Môi trường thực thi và luồng xử lý

Hệ thống nội bộ và thiết bị chạy **AI agent** được kết nối trong cùng mạng **Tailscale**. AI agent thực hiện việc điền thông tin vào mẫu và xuất file trên thiết bị thực thi.

**Mac mini chỉ là thiết bị minh họa, không phải yêu cầu bắt buộc.** Thiết bị thực thi có thể là mini PC hoặc thiết bị khác phù hợp để chạy AI agent, miễn là đáp ứng yêu cầu kết nối trong cùng mạng Tailscale.

Luồng xử lý cần thể hiện trong bài thực hành:

Hệ thống nội bộ (Laravel) tạo yêu cầu báo giá

&nbsp;&nbsp;&nbsp;&nbsp;→ Chuyển yêu cầu đến thiết bị thực thi qua mạng Tailscale

&nbsp;&nbsp;&nbsp;&nbsp;→ AI agent điền thông tin vào các vị trí tương ứng trên mẫu PowerPoint

&nbsp;&nbsp;&nbsp;&nbsp;→ Xuất file báo giá đã được điền thông tin

&nbsp;

### 1.4. Nội dung ứng viên gửi lại

Ứng viên gửi **bản kế hoạch triển khai cụ thể** và **bản demo** để thể hiện cách giải quyết bài toán.

Bản kế hoạch cần giải thích cách tiếp nhận yêu cầu từ hệ thống, chuyển thông tin đến AI agent và xác định các vị trí cần điền trên mẫu. Bản demo cần minh họa luồng xử lý từ yêu cầu báo giá đến file đầu ra. Những phần đã thực hiện, đang mô phỏng hoặc chưa hoàn thiện cần được trình bày rõ để người đánh giá hiểu đúng phạm vi bài làm.

## 2\. Công việc và trách nhiệm chính

### 2.1. Duy trì và phát triển hệ thống quản lý nội bộ

Tham gia duy trì hệ thống hiện có và phát triển thêm các tính năng phục vụ hoạt động của công ty. Công việc cụ thể sẽ được hướng dẫn trong quá trình thực hiện.

Các công nghệ đang được sử dụng trong hệ thống:

| Nhóm | Công nghệ |
| :---- | :---- |
| Backend | PHP, Laravel |
| Kết nối backend và frontend | Inertia Laravel, Inertia React |
| Frontend | React / React DOM, TypeScript |
| Công cụ xây dựng và giao diện | Vite, Tailwind CSS / Vite plugin |
| Cơ sở dữ liệu | PostgreSQL |

&nbsp;

### 2.2. Tự động hóa việc tạo văn bản

Tham gia xây dựng các quy trình tự động tạo văn bản từ dữ liệu và mẫu có sẵn, trước mắt tập trung vào báo giá, hợp đồng và các tài liệu phục vụ công việc. Bài thực hành tạo báo giá ở phần trên là một ví dụ của nhóm công việc này.

### 2.3. Thiết kế lại website giới thiệu sản phẩm

Tham gia thiết kế lại website công khai giới thiệu sản phẩm của công ty. Công nghệ được sử dụng cho website gồm **Astro** và **Cloudflare**.

### 2.4. Tích hợp AI vào các hoạt động kinh doanh

Tham gia tích hợp AI để hỗ trợ và tự động hóa các công việc liên quan đến quảng cáo, marketing và chăm sóc khách hàng. Nội dung triển khai cụ thể sẽ được hướng dẫn theo từng công việc.