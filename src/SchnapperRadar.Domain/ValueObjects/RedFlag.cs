using SchnapperRadar.Domain.Enums;

namespace SchnapperRadar.Domain.ValueObjects;

public sealed record RedFlag(string Code, string Message, RedFlagSeverity Severity);
