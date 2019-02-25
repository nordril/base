using Nordril.Functional;
using Nordril.Functional.Category;
using Nordril.Functional.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nordril.Base.Tests
{
    public class ResultTests
    {
        public static IEnumerable<object[]> MapTestOkData()
        {
            yield return new object[]
            {
                Result.Ok(0), 
                (Func<int, double>)(x => x*2D),
                0
            };
            yield return new object[]
            {
                Result.Ok(3),
                (Func<int, double>)(x => x*6D),
                18D
            };
        }

        public static IEnumerable<object[]> MapTestWithErrorsData()
        {
            yield return new object[]
            {
                Result.WithErrors<int>(new Error[] { }, ResultClass.DataConflict),
                (Func<int, double>)(x => x*2D),
            };
            yield return new object[]
            {
                Result.WithErrors<int>(new [] { new Error("uh-oh!", Code.Green, "that field", new ArgumentException("uh-oh!")) }, ResultClass.DataConflict),
                (Func<int, double>)(x => x*2D),
            };
            yield return new object[]
            {
                Result.WithErrors<int>(new [] {
                    new Error("uh-oh!", Code.Green, "that field", new ArgumentException("uh-oh!")),
                    new Error("yikes!", Code.Red, "that other field", new ArgumentException("yikes!"))

                }, ResultClass.DataConflict),
                (Func<int, double>)(x => x*2D),
            };
        }

        public static IEnumerable<object[]> ApTestData()
        {
            FuncList<int> mk(int x) => (FuncList<int>)Enumerable.Range(0, x).ToFuncList();
            var errors = new[] {
                new Error("booh!", Code.Red, "some field", new ArgumentException("booh!")),
                new Error("ouch!", Code.Orange, "some other field", new ArgumentException("ouch!"))
            };
            var errors2 = new[] {
                new Error("shablam!", Code.Red, "some 3rd field", new ArgumentException("shablam!")),
                new Error("frigg!", Code.Orange, "some 4th field", new ArgumentException("frigg!"))
            };

            yield return new object[]
            {
                Result.Ok(5),
                Result.Ok<Func<int, FuncList<int>>>(mk),
                Result.Ok(mk(5))
            };

            yield return new object[]
            {
                Result.WithErrors<int>(errors, ResultClass.BadRequest),
                Result.Ok<Func<int, FuncList<int>>>(mk),
                Result.WithErrors<FuncList<int>>(errors, ResultClass.BadRequest)
            };

            yield return new object[]
            {
                Result.Ok(5),
                Result.WithErrors<Func<int, FuncList<int>>>(errors, ResultClass.BadRequest),
                Result.WithErrors<FuncList<int>>(errors, ResultClass.BadRequest)
            };

            yield return new object[]
            {
                Result.WithErrors<int>(errors, ResultClass.EditConflict),
                Result.WithErrors<Func<int, FuncList<int>>>(errors2, ResultClass.BadRequest),
                Result.WithErrors<FuncList<int>>(errors.Concat(errors2), ResultClass.BadRequest)
            };
        }

        public static IEnumerable<object[]> BindTestData()
        {
            FuncList<int> mk(int x) => (FuncList<int>)Enumerable.Range(0, x).ToFuncList();
            var errors = new[] {
                new Error("booh!", Code.Red, "some field", new ArgumentException("booh!")),
                new Error("ouch!", Code.Orange, "some other field", new ArgumentException("ouch!"))
            };
            var errors2 = new[] {
                new Error("shablam!", Code.Red, "some 3rd field", new ArgumentException("shablam!")),
                new Error("frigg!", Code.Orange, "some 4th field", new ArgumentException("frigg!"))
            };
            Func<int, IMonad<FuncList<int>>> ok = x => Result.Ok((FuncList<int>)Enumerable.Range(0, x).ToFuncList());
            Func<int, IMonad<FuncList<int>>> err = x => Result.WithErrors<FuncList<int>>(errors2, ResultClass.BadRequest);

            yield return new object[]
            {
                Result.Ok(5),
                ok,
                Result.Ok(mk(5))
            };

            yield return new object[]
            {
                Result.WithErrors<int>(errors, ResultClass.BadRequest),
                ok,
                Result.WithErrors<FuncList<int>>(errors, ResultClass.BadRequest)
            };

            yield return new object[]
            {
                Result.Ok(5),
                err,
                Result.WithErrors<FuncList<int>>(errors, ResultClass.BadRequest)
            };

            yield return new object[]
            {
                Result.WithErrors<int>(errors, ResultClass.DataConflict),
                err,
                Result.WithErrors<FuncList<int>>(errors, ResultClass.DataConflict)
            };
        }

        [Theory]
        [MemberData(nameof(MapTestOkData))]
        public void MapTestOk(Result<int> r, Func<int, double> f, double expected)
        {
            var actual = r.Map(f);

            Assert.IsType<Result<double>>(actual);

            var actualRes = (Result<double>)actual;

            Assert.True(actualRes.IsOk);
            Assert.Equal(ResultClass.Ok, actualRes.ResultClass);
            Assert.False(ReferenceEquals(actualRes, r));
            Assert.Equal(expected, actualRes.Value());
        }

        [Theory]
        [MemberData(nameof(MapTestWithErrorsData))]
        public void MapTestWithErrors(Result<int> r, Func<int, double> f)
        {
            var errors = r.Errors();
            var resClass = r.ResultClass;
            var actual = r.Map(f);

            Assert.IsType<Result<double>>(actual);

            var actualRes = (Result<double>)actual;

            Assert.False(actualRes.IsOk);
            Assert.Equal(resClass, actualRes.ResultClass);
            Assert.False(ReferenceEquals(actualRes, r));
            Assert.Equal(errors, actualRes.Errors());
        }

        [Fact]
        public void PureTest()
        {
            var res = 7.PureUnsafe<int, Result<int>>();

            Assert.True(res.IsOk);
            Assert.Equal(ResultClass.Ok, res.ResultClass);
            Assert.Equal(7, res.Value());
        }


        [Theory]
        [MemberData(nameof(ApTestData))]
        public void ApTest(Result<int> r1, Result<Func<int, FuncList<int>>> r2, Result<FuncList<int>> expected)
        {
            var actual = r1.Ap(r2);

            Assert.IsType<Result<FuncList<int>>>(actual);

            var actualRes = (Result<FuncList<int>>)actual;

            Assert.Equal(expected.IsOk, actualRes.IsOk);
            Assert.Equal(expected.ResultClass, actualRes.ResultClass);

            Assert.False(ReferenceEquals(r1, actual));
            Assert.False(ReferenceEquals(r2, actual));

            if (expected.IsOk)
                Assert.Equal(expected.Value(), actualRes.Value());
            else
                Assert.Equal(expected.Errors(), actualRes.Errors());
        }

        [Theory]
        [MemberData(nameof(BindTestData))]
        public void BindTest(Result<int> r1, Func<int, IMonad<FuncList<int>>> f, Result<FuncList<int>> expected)
        {
            var actual = r1.Bind(f);

            Assert.IsType<Result<FuncList<int>>>(actual);

            var actualRes = (Result<FuncList<int>>)actual;

            Assert.Equal(expected.IsOk, actualRes.IsOk);
            Assert.Equal(expected.ResultClass, actualRes.ResultClass);

            Assert.False(ReferenceEquals(r1, actual));

            if (expected.IsOk)
                Assert.Equal(expected.Value(), actualRes.Value());
            else
                Assert.Equal(expected.Errors(), actualRes.Errors());
        }

        private enum Code { Green, Yellow, Orange, Red }
    }
}
