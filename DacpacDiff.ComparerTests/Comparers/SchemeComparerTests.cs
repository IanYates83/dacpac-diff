﻿using DacpacDiff.Core.Diff;
using DacpacDiff.Core.Model;
using DacpacDiff.Core.Output;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DacpacDiff.Comparer.Comparers.Tests
{
    [TestClass]
    public class SchemeComparerTests
    {
        public class TestModel : IModel, IDependentModel
        {
            public string Name { get; set; } = string.Empty;

            public string[] Dependencies { get; set; } = Array.Empty<string>();
        }
        public class TestDiff : IDifference
        {
            public IModel? Model { get; init; }

            [ExcludeFromCodeCoverage(Justification = "Not used")]
            public string? Title => throw new NotImplementedException();

            public string Name => Model?.Name ?? string.Empty;
        }

        [TestMethod]
        public void Compare__Compares_left_to_right()
        {
            // Arrange
            var lftScheme = new SchemeModel("left");
            var lftDb = new DatabaseModel("left");
            lftScheme.Databases["left"] = lftDb;

            var rgtScheme = new SchemeModel("right");
            var rgtDb = new DatabaseModel("right");
            rgtScheme.Databases["right"] = rgtDb;

            var diffMock = new Mock<IDifference>();

            var comparerMock = new Mock<IModelComparer<DatabaseModel>>(MockBehavior.Strict);
            comparerMock.Setup(m => m.Compare(lftDb, rgtDb))
                .Returns(new[] { diffMock.Object });

            var comparerFactMock = new Mock<IModelComparerFactory>(MockBehavior.Strict);
            comparerFactMock.Setup(m => m.GetComparer<DatabaseModel>())
                .Returns(comparerMock.Object);

            var comparer = new SchemeComparer(comparerFactMock.Object);

            // Act
            var res = comparer.Compare(lftScheme, rgtScheme);

            // Assert
            Assert.AreSame(diffMock.Object, res.Single());
        }

        [TestMethod]
        public void Compare__Only_supports_single_left_database()
        {
            // Arrange
            var lftScheme = new SchemeModel("left");
            var lftDb = new DatabaseModel("left");
            lftScheme.Databases["leftA"] = lftDb;
            lftScheme.Databases["leftB"] = lftDb;

            var rgtScheme = new SchemeModel("right");
            var rgtDb = new DatabaseModel("right");
            rgtScheme.Databases["right"] = rgtDb;

            var diffMock = new Mock<IDifference>();

            var comparerMock = new Mock<IModelComparer<DatabaseModel>>(MockBehavior.Strict);
            comparerMock.Setup(m => m.Compare(lftDb, rgtDb))
                .Returns(new[] { diffMock.Object });

            var comparerFactMock = new Mock<IModelComparerFactory>(MockBehavior.Strict);
            comparerFactMock.Setup(m => m.GetComparer<DatabaseModel>())
                .Returns(comparerMock.Object);

            var comparer = new SchemeComparer(comparerFactMock.Object);

            // Act
            Assert.ThrowsException<NotSupportedException>(() =>
            {
                comparer.Compare(lftScheme, rgtScheme);
            });
        }

        [TestMethod]
        public void Compare__Only_supports_single_right_database()
        {
            // Arrange
            var lftScheme = new SchemeModel("left");
            var lftDb = new DatabaseModel("left");
            lftScheme.Databases["left"] = lftDb;

            var rgtScheme = new SchemeModel("right");
            var rgtDb = new DatabaseModel("right");
            rgtScheme.Databases["rightA"] = rgtDb;
            rgtScheme.Databases["rightB"] = rgtDb;

            var diffMock = new Mock<IDifference>();

            var comparerMock = new Mock<IModelComparer<DatabaseModel>>(MockBehavior.Strict);
            comparerMock.Setup(m => m.Compare(lftDb, rgtDb))
                .Returns(new[] { diffMock.Object });

            var comparerFactMock = new Mock<IModelComparerFactory>(MockBehavior.Strict);
            comparerFactMock.Setup(m => m.GetComparer<DatabaseModel>())
                .Returns(comparerMock.Object);

            var comparer = new SchemeComparer(comparerFactMock.Object);

            // Act
            Assert.ThrowsException<NotSupportedException>(() =>
            {
                comparer.Compare(lftScheme, rgtScheme);
            });
        }

        [TestMethod]
        public void ReferencesRemain__List_contains_dependency_other_than_self__True()
        {
            // Arrange
            var diff = new TestDiff
            {
                Model = new TestModel
                {
                    Name = "TestDiff",
                    Dependencies = new[] { "TestDep" }
                }
            };

            var deps = new IDifference[]
            {
                diff, // Self
                new TestDiff { Model = new TestModel { Name = "TestDep" } }
            };

            // Act
            var res = SchemeComparer.ReferencesRemain(deps, diff);

            // Assert
            Assert.IsTrue(res);
        }

        [TestMethod]
        public void ReferencesRemain__List_contains_only_self_dependency__False()
        {
            // Arrange
            var diff = new TestDiff
            {
                Model = new TestModel
                {
                    Name = "TestDiff",
                    Dependencies = new[] { "TestDep" }
                }
            };

            var deps = new IDifference[]
            {
                diff, // Self
                new TestDiff { Model = new TestModel { Name = "TestDepX" } }
            };

            // Act
            var res = SchemeComparer.ReferencesRemain(deps, diff);

            // Assert
            Assert.IsFalse(res);
        }

        [TestMethod]
        public void OrderDiffsByDependency__Orders_diffs_based_on_dependencies()
        {
            // Arrange
            var diffA = new TestDiff { Model = new TestModel { Name = "DiffA" } };
            ((TestModel)diffA.Model).Dependencies = new[] { "DiffB" };

            var diffB = new TestDiff { Model = new TestModel { Name = "DiffB" } };
            var diffC = new TestDiff { Model = new TestModel { Name = "DiffC" } };

            var diffs = new IDifference[]
            {
                diffA,
                diffB,
                diffC
            };

            // Act
            var res = SchemeComparer.OrderDiffsByDependency(diffs).ToArray();

            // Assert
            CollectionAssert.AreEqual(new ISqlFormattable[]
            {
                diffB,
                diffC,
                diffA,
            }, res);
        }
    }
}