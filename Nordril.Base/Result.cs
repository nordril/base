﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Globalization;
using Nordril.Functional.Data;
using Nordril.Functional.Category;
using Nordril.Functional;
using System.Linq;

namespace Nordril.Base
{
    /// <summary>
    /// The result of a service call; a container for <see cref="Either{TLeft, TRight}"/>, containing either a list of errors or a result <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result, if the call was successful.</typeparam>
    public struct Result<T> : IEquatable<Result<T>>, IMonad<T>, IMonoFunctor<Result<T>, T>
    {
        /// <summary>
        /// The underlying either.
        /// </summary>
        public Either<IList<Error>, T> InnerResult { get; set; }

        /// <summary>
        /// Gets or sets the general class of result, which allows a rough categorization.
        /// </summary>
        public ResultClass ResultClass { get; set; }

        /// <summary>
        /// Returns true iff there is a result, i.e. if the underlying <see cref="Either{TLeft, TRight}.IsRight"/> is true.
        /// </summary>
        public bool IsOk => InnerResult.IsRight;

        /// <summary>
        /// Returns the value of the result, if present. If there's no result, a <see cref="PatternMatchException"/> is thrown.
        /// </summary>
        /// <exception cref="PatternMatchException">If there's no result and only errors.</exception>
        public T Value() => InnerResult.Right();

        /// <summary>
        /// Returns the errors, if present. If there are no errors, a <see cref="PatternMatchException"/> is thrown.
        /// </summary>
        /// <exception cref="PatternMatchException">If there are no errors.</exception>
        public IList<Error> Errors() => InnerResult.Left();

        /// <summary>
        /// Returns true if <see cref="IsOk"/> is false and if <see cref="Errors"/> contains an error with an inner exception of type <typeparamref name="TError"/>. This is a useful analogue for <c>catch</c>.
        /// </summary>
        /// <typeparam name="TError">The type of the exception to search for.</typeparam>
        /// <param name="error">The first occurrence of an <see cref="Error"/> which has an inner exception of type <typeparamref name="TError"/>.</param>
        public bool HasError<TError>(out Error error)
        {
            error = default;

            if (IsOk)
                return false;
            else
            {
                error = Errors()
                    .FirstMaybe(err => err.InnerException.ValueOr(ex => ex is TError, false))
                    .ValueOr(default);
                return true;
            }
        }

        /// <summary>
        /// Returns true if <see cref="IsOk"/> is false and if <see cref="Errors"/> contains an error with error code <paramref name="code"/>. This is a useful analogue for <c>catch</c>.
        /// </summary>
        /// <typeparam name="TCode">The type of the error code enumeration.</typeparam>
        /// <param name="code">The error code to search for.</param>
        /// <param name="error">The first occurrence of an <see cref="Error"/> which has an error code <paramref name="code"/>.</param>
        public bool HasErrorCode<TCode>(TCode code, out Error error)
            where TCode : Enum
            => HasErrorCode((Convert.ToInt32(code)).ToString(CultureInfo.InvariantCulture), out error);

        /// <summary>
        /// Returns true if <see cref="IsOk"/> is false and if <see cref="Errors"/> contains an error with error code <paramref name="code"/>. This is a useful analogue for <c>catch</c>.
        /// </summary>
        /// <param name="code">The error code to search for.</param>
        /// <param name="error">The first occurrence of an <see cref="Error"/> which has an error code <paramref name="code"/>.</param>
        public bool HasErrorCode(string code, out Error error)
        {
            error = default;

            if (IsOk)
                return false;
            else
            {
                error = Errors().FirstMaybe(err => err.Code == code).ValueOr(default);
                return true;
            }
        }

        /// <summary>
        /// Creates a new <see cref="Result{T}"/> from an <see cref="Either{TLeft, TRight}"/>.
        /// If <paramref name="resultClass"/> is <see cref="ResultClass.Ok"/>, <paramref name="innerResult"/> MUST contain a right-value, otherwise, an exception is thrown.
        /// </summary>
        /// <param name="innerResult">The underlying data.</param>
        /// <param name="resultClass">The result class.</param>
        /// <exception cref="ArgumentException">If <paramref name="resultClass"/> is <see cref="ResultClass.Ok"/>, but <paramref name="innerResult"/> contains no value.</exception>
        public Result(Either<IList<Error>, T> innerResult, ResultClass resultClass = ResultClass.Unspecified)
        {
            InnerResult = innerResult;
            ResultClass = resultClass;

            if (resultClass == ResultClass.Ok && innerResult.IsLeft)
                throw new ArgumentException($"Cannot create an instance of {nameof(Result<object>)} with {nameof(resultClass)}={nameof(ResultClass)}.{nameof(ResultClass.Ok)} and no value.", nameof(innerResult));
        }

        /// <summary>
        /// Creates an OK-result from a value.
        /// </summary>
        /// <param name="result">The value.</param>
        public static Result<T> Ok(T result)
            => new Result<T>(Either<IList<Error>, T>.FromRight(result), ResultClass.Ok);

        /// <summary>
        /// Creates an error-result from a list of errors.
        /// </summary>
        /// <param name="errors">The list of errors.</param>
        /// <param name="resultClass">The class of the result.</param>
        public static Result<T> WithErrors(IEnumerable<Error> errors, ResultClass resultClass)
            => new Result<T>(Either<IList<Error>, T>.FromLeft(new List<Error>(errors)), resultClass);

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Result<T>))
                return false;

            var thatResult = (Result<T>)obj;

            return (InnerResult.Equals(thatResult.InnerResult)
                && ResultClass == thatResult.ResultClass);
        }

        /// <inheritdoc />
        public override int GetHashCode() => this.DefaultHash(InnerResult, ResultClass);

        /// <inheritdoc />
        public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

        /// <inheritdoc />
        public static bool operator !=(Result<T> left, Result<T> right) => !(left == right);

        /// <inheritdoc />
        public bool Equals(Result<T> other) => Equals((object)other);

        /// <inheritdoc />
        public IFunctor<TResult> Map<TResult>(Func<T, TResult> f)
            => new Result<TResult>((Either<IList<Error>, TResult>)InnerResult.Map(f), ResultClass);

        /// <inheritdoc />
        public Result<T> MonoMap(Func<T, T> f)
            => new Result<T>((Either<IList<Error>, T>) InnerResult.Map(f), ResultClass);

        /// <inheritdoc />
        public IMonad<TResult> Bind<TResult>(Func<T, IMonad<TResult>> f)
        {
            if (IsOk)
                return f(InnerResult.Right());
            else
                return Result.WithErrors<TResult>(Errors(), ResultClass);
        }

        /// <inheritdoc />
        public IApplicative<TResult> Pure<TResult>(TResult x) => Result.Ok(x);

        /// <inheritdoc />
        public IApplicative<TResult> Ap<TResult>(IApplicative<Func<T, TResult>> f)
        {
            if (f == null || !(f is Result<Func<T, TResult>>))
                throw new InvalidCastException();

            var fResult = (Result<Func<T, TResult>>)f;

            if (IsOk || fResult.IsOk)
                return new Result<TResult>((Either<IList<Error>, TResult>)InnerResult.Ap(fResult.InnerResult), fResult.ResultClass);
            else
                return new Result<TResult>(Either.FromLeft<IList<Error>, TResult>(InnerResult.Left().Concat(fResult.InnerResult.Left()).ToList()), fResult.ResultClass);
        }
    }

    /// <summary>
    /// Extension methods for <see cref="Result{T}"/>.
    /// </summary>
    public static class Result
    {
        /// <summary>
        /// Creates an OK-result from a value.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="result">The result.</param>
        public static Result<T> Ok<T>(T result) => Result<T>.Ok(result);

        /// <summary>
        /// Creates an error-result from a list of errors.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="errors">The list of errors.</param>
        /// <param name="resultClass">The result class.</param>
        public static Result<T> WithErrors<T>(IEnumerable<Error> errors, ResultClass resultClass) => Result<T>.WithErrors(errors, resultClass);

        /// <summary>
        /// Tries to cast a <see cref="IFunctor{TSource}"/> to a <see cref="Result{T}"/> via an explicit cast.
        /// Convenience method.
        /// </summary>
        /// <typeparam name="T">The type of the value contained in the functor.</typeparam>
        /// <param name="f">The functor to cast to a maybe.</param>
        public static Result<T> ToResult<T>(this IFunctor<T> f) => (Result<T>)f;
    }
}
