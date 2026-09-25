# Solution Plan: Tu dong hoa tao bao gia

## Muc tieu

Prototype SME cho quy trinh: nhan vien chon khach hang, nhap thong tin san pham va gui yeu cau; Web API tao `QuoteJob`; Worker tren Mac mini lay job, dien template va tra ve file bao gia.

## 1. Phan vai AI va code xac dinh

Nguyen tac: **AI de xuat, code quyet dinh**.

| Cong viec | Cong cu | Ly do |
|---|---|---|
| Validate form, sinh so bao gia | Code xac dinh | Quy tac ro rang, test duoc |
| Tinh thanh tien, VAT, tong tien | Code voi `decimal` | Tranh sai so tien |
| Copy template va dien placeholder/o | Code (`ClosedXML`, `python-docx` hoac `docxtemplater`) | Lap lai, on dinh, khong ton token |
| Kiem tra file, doi chieu tong tien | Code | Co the tu dong pass/fail |
| Chuan hoa ghi chu tu do | Codex CLI/LLM tuy chon | Phu hop bai toan ngon ngu |
| De xuat mapping khi template thay doi | Codex CLI o giai doan dev | Tao patch de nguoi review, khong tu chay production |

Neu AI trong runtime loi hoac timeout, giu nguyen van ban goc va tiep tuc job. Output AI phai theo JSON schema, co timeout, duoc xem input la du lieu de tranh prompt injection.

## 2. Data flow va trang thai

```mermaid
sequenceDiagram
    actor U as Nhan vien kinh doanh
    participant A as Web/API
    participant D as Database
    participant M as Worker Mac mini
    participant C as Codex CLI tuy chon
    U->>A: Gui form + IdempotencyKey
    A->>A: Validate va tinh lai tong tien
    A->>D: Tao QuoteJob PENDING
    A-->>U: Tra JobId
    M->>A: Poll va claim job
    A->>D: Claim nguyen tu, chuyen PROCESSING
    A-->>M: Payload snapshot
    M->>M: Dien template bang code
    opt Ghi chu tu do
        M->>C: Chuan hoa ghi chu
        C-->>M: JSON hop le hoac fallback van ban goc
    end
    M->>M: Kiem tra file va tong tien
    M->>A: Gui file + SHA-256
    A->>D: Chuyen COMPLETED
    U->>A: Xem trang thai va tai file
```

```mermaid
stateDiagram-v2
    [*] --> PENDING: Gui yeu cau
    PENDING --> PROCESSING: Worker claim
    PROCESSING --> COMPLETED: File hop le
    PROCESSING --> PENDING: Loi tam thoi, con retry
    PROCESSING --> FAILED: Loi vinh vien/het retry
    FAILED --> PENDING: Admin thu lai
    COMPLETED --> [*]
```

`QuoteJob` toi thieu gom: `Id`, `QuoteNo`, `CustomerId`, `PayloadJson`, `IdempotencyKey` unique, `Status`, `Attempt`, `NextAttemptAt`, `LockedUntil`, `ErrorCode`, `ErrorMessage`, `OutputPath`, `OutputSha256`, `CreatedBy` va cac moc thoi gian. Luu snapshot payload de tai tao dung file khi du lieu khach hang thay doi.

Worker pull job thay vi API push vao Mac mini: khong can mo cong Mac mini ra Internet, job van nam trong queue khi may offline; doi lai co do tre theo chu ky poll.

## 3. Reliability va security

- **Idempotency:** unique index tren `IdempotencyKey`; gui lai cung key tra ve job cu. Nut Submit disable chi la UX.
- **Retry:** toi da 3 lan, backoff 1, 5, 15 phut. Loi mang, timeout, file bi khoa la tam thoi; template thieu placeholder hoac payload sai la loi vinh vien.
- **Lease:** `LockedUntil` tu dong tra job ve `PENDING` neu Worker chet giua chung.
- **File:** ghi file tam, kiem tra, sau do doi ten atomic; endpoint ket qua idempotent.
- **Validation:** server khong tin tong tien tu client; gioi han so dong, do dai, so luong > 0, don gia >= 0.
- **Bao mat:** HTTPS, ASP.NET Identity, role Business/Admin, API key rieng cho Worker, secret trong environment, file ngoai `wwwroot`, ten file do server sinh.
- **Formula injection:** gia tri Excel bat dau bang `=`, `+`, `-`, `@` phai ghi nhu chuoi.
- **Audit:** log `JobId`, nguoi tao, nguoi tai va moc thoi gian; khong log du lieu nhay cam.

## 4. Cong nghe

- ASP.NET Core Web API: API va OpenAPI.
- .NET Worker Service: tien trinh poll/claim tren Mac mini.
- SQLite + EF Core: don gian cho prototype SME; co the doi provider sang SQL Server.
- ClosedXML: dien template XLSX va kiem tra de test.
- xUnit: unit test cho tinh tien, idempotency va mapping.
- Interface `ICodexClient` voi `MockCodexClient` cho prototype va `CliCodexClient` khi co Codex CLI that.

## 5. Pham vi prototype

**Lam that:** form/API, validate, QuoteJob, trang thai, worker polling, mapping template, kiem tra file, idempotency va retry co ban.

**Mock:** CRM, Mac mini (chay Worker local), Codex CLI.

**De sau:** PDF, email, monitoring nang cao, key rotation, backup tu dong va SQL Server.
