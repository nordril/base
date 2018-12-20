using System;
using System.Collections.Generic;
using System.Net;
using System.Globalization;
using Nordril.Functional.Data;
using Nordril.Functional.Category;
using Nordril.Functional;

namespace Nordril.Base
{
    /// <summary>
    /// The result of a service call; a container for <see cref="Either{TLeft, TRight}"/>, containing either a list of errors or a result <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result, if the call was successful.</typeparam>
    public struct Result<T> : IEquatable<Result<T>>, IFunctor<T>, IMonoFunctor<Result<T>, T>
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

    /// <summary>
    /// Indicates the general class of the result. Roughly equivalent to HTTP status codes.
    /// </summary>
    public enum ResultClass
    {
        /// <summary>
        /// The resource was already present and couldn't be inserted. Roughly equivalent to HTTP 409 (Conflict).
        /// </summary>
        AlreadyPresent,

        /// <summary>
        /// The request cannot be fulfilled because it is syntactically invalid.
        /// </summary>
        BadRequest,

        /// <summary>
        /// There was a conflict in the requested data, such as when multiple results were found but only one was expected. Equivalent to HTTP 409 (Conflict).
        /// </summary>
        DataConflict,

        /// <summary>
        /// There was a conflict with another request when trying to update the resource. Equivalent to HTTP 409 (Conflict).
        /// </summary>
        EditConflict,

        /// <summary>
        /// The request was not allowed because the user wasn't authorized to perform it. Equivalent to HTTP 403.
        /// </summary>
        Forbidden,

        /// <summary>
        /// There was an internal exception. Equivalent to HTTP 500.
        /// </summary>
        InternalException,

        /// <summary>
        /// The request was successful and there were no errors.
        /// </summary>
        Ok,

        /// <summary>
        /// The requested resource was not found. Equivalent to HTTP 404.
        /// </summary>
        NotFound,

        /// <summary>
        /// The method has not been implemented. Equivalent to HTTP 501 (Not implemented).
        /// </summary>
        NotImplemented,

        /// <summary>
        /// The resource is gone. Equivalent to HTTP 410 (Gone) and similar to HTTP 404.
        /// </summary>
        ResourceGone,

        /// <summary>
        /// The request couldn't be processed because it was semantically invalid in a way not described by other <see cref="ResultClass"/>-members. This is the default <see cref="ResultClass"/> for input error.
        /// </summary>
        UnprocessableEntity,

        /// <summary>
        /// Unspecified error result, indicating that there was an error, but that we were not able to specify which kind. Should be avoided.
        /// </summary>
        Unspecified,
    }

    /// <summary>
    /// Extension methods for <see cref="ResultClass"/>.
    /// </summary>
    public static class ResultClassExtensions
    {
        /// <summary>
        /// Converts a <see cref="ResultClass"/> to the nearest approximate HTTP status code.
        /// </summary>
        /// <param name="r">The result class to convert.</param>
        public static HttpStatusCode ToHttpStatusCode(this ResultClass r)
        {
            switch (r)
            {
                case ResultClass.AlreadyPresent:
                    return HttpStatusCode.BadRequest;
                case ResultClass.BadRequest:
                    return HttpStatusCode.BadRequest;
                case ResultClass.DataConflict:
                    return HttpStatusCode.Conflict;
                case ResultClass.EditConflict:
                    return HttpStatusCode.Conflict;
                case ResultClass.Forbidden:
                    return HttpStatusCode.Forbidden;
                case ResultClass.InternalException:
                    return HttpStatusCode.InternalServerError;
                case ResultClass.NotFound:
                    return HttpStatusCode.NotFound;
                case ResultClass.NotImplemented:
                    return HttpStatusCode.NotImplemented;
                case ResultClass.Ok:
                    return HttpStatusCode.OK;
                case ResultClass.ResourceGone:
                    return HttpStatusCode.Gone;
                case ResultClass.UnprocessableEntity:
                    return HttpStatusCode.UnprocessableEntity;
                case ResultClass.Unspecified:
                    return HttpStatusCode.InternalServerError;
                default:
                    throw new ArgumentException($"Unknown {nameof(ResultClass)} value {r}.", nameof(r));
            }
        }
    }
}
