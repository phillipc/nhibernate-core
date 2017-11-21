﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by AsyncGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------


using System;
using System.Linq;
using NHibernate.Cfg.MappingSchema;
using NHibernate.Criterion;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Exceptions;
using NHibernate.Mapping.ByCode;
using NUnit.Framework;
using NHibernate.Linq;

namespace NHibernate.Test.TypesTest
{
	using System.Threading.Tasks;
	using System.Threading;
	/// <summary>
	/// Various tests regarding handling of size of query parameters.
	/// </summary>
	[TestFixture]
	public class StringTypeWithLengthFixtureAsync : TestCaseMappingByCode
	{
		private int GetLongStringMappedLength()
		{
			// This is a bit ugly...
			//
			// Return a value that should be the largest possible length of a string column
			// in the corresponding database. Note that the actual column type selected by the dialect
			// depends on this value, so it must be the largest possible value for the type
			// that the dialect will pick. Doesn't matter if the dialect can pick another
			// type for an even larger size.

			if (Dialect is Oracle8iDialect)
				return 2000;

			if (Dialect is MySQLDialect)
				return 65535;

			return 4000;
		}

		protected override HbmMapping GetMappings()
		{
			var mapper = new ModelMapper();
			mapper.Class<StringClass>(ca =>
			{
				ca.Lazy(false);
				ca.Id(x => x.Id, map => map.Generator(Generators.Assigned));
				ca.Property(x => x.StringValue, map => map.Length(10));
				ca.Property(x => x.LongStringValue, map => map.Length(GetLongStringMappedLength()));
			});

			return mapper.CompileMappingForAllExplicitlyAddedEntities();
		}

		protected override void OnTearDown()
		{
			base.OnTearDown();
			using (var s = OpenSession())
			using (var t = s.BeginTransaction())
			{
				s.CreateQuery("delete from System.Object").ExecuteUpdate();
				t.Commit();
			}
		}

		[Test]
		[Description("Values longer than the maximum possible string length " +
		             "should raise an exception if they would otherwise be truncated.")]
		public Task ShouldPreventInsertionOfVeryLongStringThatWouldBeTruncatedAsync()
		{
			try
			{
				// This test case is for when the current driver will use a parameter size
				// that is significantly larger than the mapped column size (e.g. SqlClientDriver currently).

				// Note: This test could possible be written as
				//   "database must raise an error OR it must store and return the full value"
				// to avoid this dialect specific exception.
				if (Dialect is SQLiteDialect)
					Assert.Ignore("SQLite does not enforce specified string lengths.");

				int maxStringLength = GetLongStringMappedLength();

				var ex = Assert.CatchAsync<Exception>(
				async () =>
					{
						using (ISession s = OpenSession())
						{
							StringClass b = new StringClass {LongStringValue = new string('x', maxStringLength + 1)};
							await (s.SaveAsync(b));
							await (s.FlushAsync());
						}
					});

				return AssertFailedInsertExceptionDetailsAndEmptyTableAsync(ex);
			}
			catch (Exception ex)
			{
				return Task.FromException<object>(ex);
			}
		}

		// NH-4083
		[Test]
		public async Task CanCompareShortValueWithLongStringAsync()
		{
			var maxStringLength = GetLongStringMappedLength();
			var longString = new string('x', maxStringLength);
			using (var s = OpenSession())
			{
				var b = new StringClass { LongStringValue = longString };
				await (s.SaveAsync(b));
				await (s.FlushAsync());
			}

			using (var s = OpenSession())
			{
				var q = s.CreateQuery("from StringClass s where s.LongStringValue != :shortString")
				         // Do not replace with SetString, otherwise length will be unspecified.
				         .SetParameter("shortString", "aaa");
				var sc = await (q.UniqueResultAsync<StringClass>());
				Assert.That(sc, Is.Not.Null);
				Assert.That(sc.LongStringValue, Is.EqualTo(longString));
			}
		}

		[Test]
		public async Task CanCompareLongValueWithLongStringAsync()
		{
			var maxStringLength = GetLongStringMappedLength();

			if (Sfi.ConnectionProvider.Driver is OdbcDriver && maxStringLength >= 2000)
				Assert.Ignore("Odbc wrecks nvarchar parameter types when they are longer than 2000, it switch them to ntext");

			var longString = new string('x', maxStringLength);
			using (var s = OpenSession())
			{
				var b = new StringClass { LongStringValue = longString };
				await (s.SaveAsync(b));
				await (s.FlushAsync());
			}

			using (var s = OpenSession())
			{
				var q = s.CreateQuery("from StringClass s where s.LongStringValue = :longString")
				         // Do not replace with SetString, otherwise length will be unspecified.
				         .SetParameter("longString", longString);
				var sc = await (q.UniqueResultAsync<StringClass>());
				Assert.That(sc, Is.Not.Null);
				Assert.That(sc.LongStringValue, Is.EqualTo(longString));
			}
		}

		[Test]
		[Description("Values longer than the mapped string length " +
		             "should raise an exception if they would otherwise be truncated.")]
		public Task ShouldPreventInsertionOfTooLongStringThatWouldBeTruncatedAsync()
		{
			try
			{
				// Note: This test could possible be written as
				//   "database must raise an error OR it must store and return the full value"
				// to avoid this dialect specific exception.
				if (Dialect is SQLiteDialect)
					Assert.Ignore("SQLite does not enforce specified string lengths.");

				var ex = Assert.CatchAsync<Exception>(
				async () =>
					{
						using (ISession s = OpenSession())
						{
							StringClass b = new StringClass {StringValue = "0123456789a"};
							await (s.SaveAsync(b));
							await (s.FlushAsync());
						}
					},
				"An exception was expected when trying to put too large a value into a column.");

				return AssertFailedInsertExceptionDetailsAndEmptyTableAsync(ex);
			}
			catch (Exception ex)
			{
				return Task.FromException<object>(ex);
			}
		}

		private async Task AssertFailedInsertExceptionDetailsAndEmptyTableAsync(Exception ex, CancellationToken cancellationToken = default(CancellationToken))
		{
			// We can get different sort of exceptions.
			if (ex is PropertyValueException)
			{
				// Some drivers/dialects set explicit parameter sizes, in which case we expect NH to
				// raise a PropertyValueException (to avoid ADO.NET from silently truncating).

				Assert.That(
					ex.Message,
					Does.StartWith("Error dehydrating property value for NHibernate.Test.TypesTest.StringClass."));

				Assert.That(ex.InnerException, Is.TypeOf<HibernateException>());

				Assert.That(
					ex.InnerException.Message,
					Is.EqualTo("The length of the string value exceeds the length configured in the mapping/parameter."));
			}
			else if (Dialect is MsSqlCeDialect && ex is InvalidOperationException)
			{
				Assert.That(ex.Message, Does.Contain("max=4000, len=4001"));
			}
			else
			{
				// In other cases, we expect the database itself to raise an error. This case
				// will also happen if the driver does set an explicit parameter size, but that
				// size is larger than the mapped column size.
				Assert.That(ex, Is.TypeOf<GenericADOException>());
			}

			// In any case, nothing should have been inserted.
			using (ISession s = OpenSession())
			{
				Assert.That(await (s.Query<StringClass>().ToListAsync(cancellationToken)), Is.Empty);
			}
		}

		[Test]
		public async Task CriteriaLikeParameterCanExceedColumnSizeAsync()
		{
			using (ISession s = OpenSession())
			using (s.BeginTransaction())
			{
				await (s.SaveAsync(new StringClass {Id = 1, StringValue = "AAAAAAAAAB"}));
				await (s.SaveAsync(new StringClass {Id = 2, StringValue = "BAAAAAAAAA"}));

				var aaItems =
					await (s.CreateCriteria<StringClass>()
					 .Add(Restrictions.Like("StringValue", "%AAAAAAAAA%"))
					 .ListAsync());

				Assert.That(aaItems.Count, Is.EqualTo(2));
			}
		}

		[Test]
		public async Task HqlLikeParameterCanExceedColumnSizeAsync()
		{
			using (ISession s = OpenSession())
			using (s.BeginTransaction())
			{
				await (s.SaveAsync(new StringClass {Id = 1, StringValue = "AAAAAAAAAB"}));
				await (s.SaveAsync(new StringClass {Id = 2, StringValue = "BAAAAAAAAA"}));

				var aaItems =
					await (s.CreateQuery("from StringClass s where s.StringValue like :likeValue")
					 .SetParameter("likeValue", "%AAAAAAAAA%")
					 .ListAsync());

				Assert.That(aaItems.Count, Is.EqualTo(2));
			}
		}

		[Test]
		public async Task CriteriaEqualityParameterCanExceedColumnSizeAsync()
		{
			if (!TestDialect.SupportsNonDataBoundCondition)
			{
				// Doesn't work on Firebird due to Firebird not figuring out parameter types on its own.
				Assert.Ignore("Dialect does not support this test");
			}

			// We should be able to query a column with a value longer than
			// the specified column size, to avoid tedious exceptions.
			using (ISession s = OpenSession())
			using (s.BeginTransaction())
			{
				await (s.SaveAsync(new StringClass {Id = 1, StringValue = "AAAAAAAAAB"}));
				await (s.SaveAsync(new StringClass {Id = 2, StringValue = "BAAAAAAAAA"}));

				var aaItems =
					await (s.CreateCriteria<StringClass>()
					 .Add(Restrictions.Eq("StringValue", "AAAAAAAAABx"))
					 .ListAsync());

				Assert.That(aaItems.Count, Is.EqualTo(0));
			}
		}

		[Test]
		public async Task HqlEqualityParameterCanExceedColumnSizeAsync()
		{
			if (!TestDialect.SupportsNonDataBoundCondition)
			{
				// Doesn't work on Firebird due to Firebird not figuring out parameter types on its own.
				Assert.Ignore("Dialect does not support this test");
			}

			// We should be able to query a column with a value longer than
			// the specified column size, to avoid tedious exceptions.
			using (ISession s = OpenSession())
			using (s.BeginTransaction())
			{
				await (s.SaveAsync(new StringClass {Id = 1, StringValue = "AAAAAAAAAB"}));
				await (s.SaveAsync(new StringClass {Id = 2, StringValue = "BAAAAAAAAA"}));

				var aaItems =
					await (s.CreateQuery("from StringClass s where s.StringValue = :likeValue")
					 .SetParameter("likeValue", "AAAAAAAAABx")
					 .ListAsync());

				Assert.That(aaItems.Count, Is.EqualTo(0));
			}
		}
	}
}