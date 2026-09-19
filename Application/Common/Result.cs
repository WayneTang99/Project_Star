using System;

namespace Project_Star.Application.Common;

/// <summary>Represents the synchronous outcome of an application operation.</summary>
public class Result
{
    protected Result(bool isSuccess, Failure? failure)
    {
        if (isSuccess == (failure is not null))
        {
            throw new ArgumentException("A successful result cannot have a failure, and a failed result must have one.");
        }

        IsSuccess = isSuccess;
        Failure = failure;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Failure? Failure { get; }

    public static Result Success() => new(true, null);

    public static Result Fail(Failure failure) => new(false, failure ?? throw new ArgumentNullException(nameof(failure)));
}

/// <summary>Represents the synchronous outcome of an application operation with a value.</summary>
public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, Failure? failure)
        : base(isSuccess, failure)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static new Result<T> Fail(Failure failure) => new(false, default, failure ?? throw new ArgumentNullException(nameof(failure)));
}
