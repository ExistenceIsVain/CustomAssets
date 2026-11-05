using System;

public interface IReturnResult<TResult>
{
    event Action<TResult> OnResult;
}