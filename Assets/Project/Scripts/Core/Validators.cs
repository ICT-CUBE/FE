using System.Text.RegularExpressions;

namespace ARZone
{
    // 입력 검증 결과. 3상태로 나눈 이유:
    //   Empty  = 아직 안 쳤음 → 에러 문구 숨김, 버튼은 비활성
    //   Invalid= 틀림        → 에러 문구 표시, 버튼 비활성
    //   Valid  = 통과        → 문구 숨김, 버튼 활성 가능
    public enum ValidationState { Empty, Invalid, Valid }

    public readonly struct ValidationResult
    {
        public readonly ValidationState State;
        public readonly string Message;   // Invalid 일 때만 채워짐

        ValidationResult(ValidationState state, string message)
        {
            State = state;
            Message = message;
        }

        public bool IsValid => State == ValidationState.Valid;
        public bool ShowError => State == ValidationState.Invalid;

        public static readonly ValidationResult Empty = new(ValidationState.Empty, null);
        public static readonly ValidationResult Ok    = new(ValidationState.Valid, null);
        public static ValidationResult Fail(string message) => new(ValidationState.Invalid, message);
    }

    // UI 와 분리된 순수 검증 규칙. 화면 구조가 바뀌어도 여기는 안 바뀐다.
    public static class Validators
    {
        // 닉네임 길이 상한. 목업의 "최대 8자" 와 카운터 "0 / 10" 이 불일치 →
        // 디자이너 확인 후 이 값만 바꾸면 된다.
        public const int NicknameMaxLength = 10;
        public const int PasswordMinLength = 8;
        public const int PasswordMaxLength = 20;

        // 실용 수준의 이메일 패턴. RFC 완전 준수는 과하고 오탐만 늘어난다.
        static readonly Regex EmailPattern =
            new(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);

        // 완성형 한글 + 영문 + 숫자만. 자모 단독(ㅋㅋㅋ), 공백, 특수문자, 이모지 제외.
        static readonly Regex NicknamePattern =
            new(@"^[가-힣A-Za-z0-9]+$", RegexOptions.Compiled);

        static readonly Regex HasLetter = new(@"[A-Za-z]", RegexOptions.Compiled);
        static readonly Regex HasDigit  = new(@"[0-9]",    RegexOptions.Compiled);

        public static ValidationResult Email(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Empty;

            return EmailPattern.IsMatch(value.Trim())
                ? ValidationResult.Ok
                : ValidationResult.Fail("올바른 이메일 형식이 아닙니다.");
        }

        public static ValidationResult Password(string value)
        {
            if (string.IsNullOrEmpty(value)) return ValidationResult.Empty;

            if (value.Length < PasswordMinLength || value.Length > PasswordMaxLength)
                return ValidationResult.Fail($"{PasswordMinLength}~{PasswordMaxLength}자로 입력해주세요.");

            if (!HasLetter.IsMatch(value) || !HasDigit.IsMatch(value))
                return ValidationResult.Fail("영문과 숫자를 모두 포함해야 합니다.");

            return ValidationResult.Ok;
        }

        public static ValidationResult PasswordConfirm(string password, string confirm)
        {
            if (string.IsNullOrEmpty(confirm)) return ValidationResult.Empty;

            return confirm == password
                ? ValidationResult.Ok
                : ValidationResult.Fail("비밀번호가 일치하지 않습니다.");
        }

        public static ValidationResult Nickname(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Empty;

            var v = value.Trim();

            if (v.Length > NicknameMaxLength)
                return ValidationResult.Fail($"최대 {NicknameMaxLength}자까지 입력할 수 있습니다.");

            return NicknamePattern.IsMatch(v)
                ? ValidationResult.Ok
                : ValidationResult.Fail("한글, 영문, 숫자 조합만 사용 가능합니다.");
        }
    }
}