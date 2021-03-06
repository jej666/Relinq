// Copyright (c) rubicon IT GmbH, www.rubicon.eu
//
// See the NOTICE file distributed with this work for additional information
// regarding copyright ownership.  rubicon licenses this file to you under 
// the Apache License, Version 2.0 (the "License"); you may not use this 
// file except in compliance with the License.  You may obtain a copy of the 
// License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.  See the 
// License for the specific language governing permissions and limitations
// under the License.
// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Clauses.StreamedData;
using Remotion.Linq.Development.UnitTesting;
using Remotion.Linq.UnitTests.TestDomain;
using Rhino.Mocks;

namespace Remotion.Linq.UnitTests
{
  [TestFixture]
  public class QueryModelTest
  {
    private MainFromClause _mainFromClause;
    private SelectClause _selectClause;
    private QueryModel _queryModel;
    private QuerySourceMapping _querySourceMapping;

    [SetUp]
    public void SetUp ()
    {
      _mainFromClause = ExpressionHelper.CreateMainFromClause_Int();
      _selectClause = ExpressionHelper.CreateSelectClause();
      _queryModel = new QueryModel (_mainFromClause, _selectClause);
      _querySourceMapping = new QuerySourceMapping();
    }

    [Test]
    public void Initialize ()
    {
      Assert.That (_queryModel.MainFromClause, Is.SameAs (_mainFromClause));
      Assert.That (_queryModel.SelectClause, Is.SameAs (_selectClause));
    }

    [Test]
    public void Accept ()
    {
      var repository = new MockRepository();
      var visitorMock = repository.StrictMock<IQueryModelVisitor>();

      visitorMock.Expect (mock => mock.VisitQueryModel (_queryModel));

      repository.ReplayAll();

      _queryModel.Accept (visitorMock);

      repository.VerifyAll();
    }

    [Test]
    public void GetOutputDataInfo_FromSelectClause ()
    {
      var outputDataInfo = _queryModel.GetOutputDataInfo ();
      Assert.That (outputDataInfo, Is.InstanceOf (typeof (StreamedSequenceInfo)));
      Assert.That (outputDataInfo.DataType, Is.SameAs (typeof (IQueryable<int>)));
      Assert.That (((StreamedSequenceInfo) outputDataInfo).ItemExpression, Is.SameAs (_queryModel.SelectClause.Selector));
    }

    [Test]
    public void GetOutputDataInfo_FromResultOperator ()
    {
      _queryModel.ResultOperators.Add (new CountResultOperator ());

      var outputDataInfo = _queryModel.GetOutputDataInfo ();
      Assert.That (outputDataInfo, Is.InstanceOf (typeof (StreamedValueInfo)));
      Assert.That (outputDataInfo.DataType, Is.SameAs (typeof (int)));
    }

    [Test]
    public void GetOutputDataInfo_FromMultipleResultOperators ()
    {
      _queryModel.ResultOperators.Add (new DistinctResultOperator ());
      _queryModel.ResultOperators.Add (new SingleResultOperator (false));

      var outputDataInfo = _queryModel.GetOutputDataInfo ();
      Assert.That (outputDataInfo, Is.InstanceOf (typeof (StreamedValueInfo)));
      Assert.That (outputDataInfo.DataType, Is.SameAs (typeof (int)));
    }

    [Test]
    public void GetOutputDataInfo_WithResultTypeOverride ()
    {
      _queryModel.ResultTypeOverride = typeof (List<>);
      
      var outputDataInfo = _queryModel.GetOutputDataInfo ();
      
      Assert.That (outputDataInfo, Is.InstanceOf (typeof (StreamedSequenceInfo)));
      Assert.That (outputDataInfo.DataType, Is.SameAs (typeof (List<int>)));
      Assert.That (((StreamedSequenceInfo) outputDataInfo).ItemExpression, Is.SameAs (_queryModel.SelectClause.Selector));
    }

    [Test]
    public void GetOutputDataInfo_WithResultTypeOverride_Null ()
    {
      _queryModel.ResultTypeOverride = typeof (IEnumerable<object>);
      _queryModel.ResultTypeOverride = null;

      var outputDataInfo = _queryModel.GetOutputDataInfo ();

      Assert.That (outputDataInfo, Is.InstanceOf (typeof (StreamedSequenceInfo)));
      Assert.That (outputDataInfo.DataType, Is.SameAs (typeof (IQueryable<int>)));
      Assert.That (((StreamedSequenceInfo) outputDataInfo).ItemExpression, Is.SameAs (_queryModel.SelectClause.Selector));
    }

    [Test]
    public new void ToString ()
    {
      var queryModel = new QueryModel (new MainFromClause ("x", typeof (Cook), Expression.Constant (0)), 
          new SelectClause (Expression.Constant (0)));
      Assert.That (queryModel.ToString(), Is.EqualTo ("from Cook x in 0 select 0"));
    }

    [Test]
    public void ToString_WithBodyClauses ()
    {
      var queryModel = new QueryModel (new MainFromClause ("x", typeof (Cook), Expression.Constant (0)),
          new SelectClause (Expression.Constant (0)));
      queryModel.BodyClauses.Add (new WhereClause (Expression.Constant (false)));
      var orderByClause = new OrderByClause ();
      orderByClause.Orderings.Add (new Ordering (Expression.Constant (1), OrderingDirection.Asc));
      queryModel.BodyClauses.Add (orderByClause);
      
      Assert.That (queryModel.ToString (), Is.EqualTo ("from Cook x in 0 where False orderby 1 asc select 0"));
    }

    [Test]
    public void ToString_WithResultOperators ()
    {
      var queryModel = new QueryModel (new MainFromClause ("x", typeof (Cook), Expression.Constant (0)),
          new SelectClause (Expression.Constant (0)));
      queryModel.ResultOperators.Add (new DistinctResultOperator ());
      queryModel.ResultOperators.Add (new CountResultOperator ());

      Assert.That (queryModel.ToString (), Is.EqualTo ("from Cook x in 0 select 0 => Distinct() => Count()"));
    }

    [Test]
    public void ToString_IdentityQuery ()
    {
      var mainFromClause = new MainFromClause ("x", typeof (Cook), Expression.Constant (0));
      var queryModel = new QueryModel (mainFromClause, new SelectClause (new QuerySourceReferenceExpression (mainFromClause)));
      Assert.That (queryModel.ToString (), Is.EqualTo ("0"));
    }

    [Test]
    public void ToString_IdentityQuery_UsesFormatter ()
    {
      var referencedSource = new MainFromClause ("y", typeof (Cook), Expression.Constant (0));
      var mainFromClause = new MainFromClause ("x", typeof (Cook), new QuerySourceReferenceExpression (referencedSource));
      var queryModel = new QueryModel (mainFromClause, new SelectClause (new QuerySourceReferenceExpression (mainFromClause)));
      Assert.That (queryModel.ToString (), Is.EqualTo ("[y]"));
    }

    [Test]
    public void Clone_ReturnsNewQueryModel ()
    {
      var queryModel = ExpressionHelper.CreateQueryModel<Cook>();
      var clone = queryModel.Clone();

      Assert.That (clone, Is.Not.Null);
      Assert.That (clone, Is.Not.SameAs (queryModel));
    }

    [Test]
    public void Clone_Keeps_ResultTypeOverride ()
    {
      var queryModel = ExpressionHelper.CreateQueryModel<Cook> ();
      queryModel.ResultTypeOverride = typeof (List<Cook>);
      var clone = queryModel.Clone ();

      Assert.That (clone.ResultTypeOverride, Is.SameAs (queryModel.ResultTypeOverride));
    }

    [Test]
    public void Clone_HasCloneForMainFromClause ()
    {
      var clone = _queryModel.Clone();

      Assert.That (clone.MainFromClause, Is.Not.SameAs (_queryModel.MainFromClause));
      Assert.That (clone.MainFromClause.ItemName, Is.EqualTo (_queryModel.MainFromClause.ItemName));
      Assert.That (clone.MainFromClause.ItemType, Is.SameAs (_queryModel.MainFromClause.ItemType));
    }

    [Test]
    public void Clone_HasCloneForMainFromClause_PassesMapping ()
    {
      var clone = _queryModel.Clone (_querySourceMapping);
      Assert.That (
          ((QuerySourceReferenceExpression) _querySourceMapping.GetExpression (_queryModel.MainFromClause)).ReferencedQuerySource,
          Is.SameAs (clone.MainFromClause));
    }

    [Test]
    public void Clone_HasCloneForSelectClause ()
    {
      var selectClause = _queryModel.SelectClause;
      var clone = _queryModel.Clone();

      Assert.That (clone.SelectClause, Is.Not.SameAs (_queryModel.SelectClause));
      var cloneSelectClause = clone.SelectClause;
      Assert.That (cloneSelectClause.Selector, Is.EqualTo (selectClause.Selector));
    }

    [Test]
    public void Clone_HasCloneForSelectClause_TransformExpressions ()
    {
      var oldReferencedClause = ExpressionHelper.CreateMainFromClause_Int();
      _queryModel.SelectClause.Selector = new QuerySourceReferenceExpression (oldReferencedClause);

      var newReferenceExpression = new QuerySourceReferenceExpression (ExpressionHelper.CreateMainFromClause_Int());
      _querySourceMapping.AddMapping (oldReferencedClause, newReferenceExpression);

      var clone = _queryModel.Clone (_querySourceMapping);
      Assert.That (clone.SelectClause.Selector, Is.SameAs (newReferenceExpression));
    }

    [Test]
    public void Clone_HasClonesForBodyClauses ()
    {
      var additionalFromClause = ExpressionHelper.CreateAdditionalFromClause();
      var whereClause = ExpressionHelper.CreateWhereClause();
      _queryModel.BodyClauses.Add (additionalFromClause);
      _queryModel.BodyClauses.Add (whereClause);

      var clone = _queryModel.Clone();
      var clonedAdditionalFromClause = (AdditionalFromClause) clone.BodyClauses[0];
      var clonedWhereClause = (WhereClause) clone.BodyClauses[1];

      Assert.That (clonedAdditionalFromClause, Is.Not.SameAs (additionalFromClause));
      Assert.That (clonedAdditionalFromClause.ItemName, Is.EqualTo (additionalFromClause.ItemName));
      Assert.That (clonedAdditionalFromClause.ItemType, Is.SameAs (additionalFromClause.ItemType));
      Assert.That (clonedWhereClause, Is.Not.SameAs (whereClause));
      Assert.That (clonedWhereClause.Predicate, Is.EqualTo (whereClause.Predicate));
    }

    [Test]
    public void Clone_HasCloneForBodyClauses_TransformExpressions ()
    {
      var bodyClause = ExpressionHelper.CreateWhereClause();
      var oldReferencedClause = ExpressionHelper.CreateMainFromClause_Int();
      bodyClause.Predicate = new QuerySourceReferenceExpression (oldReferencedClause);
      _queryModel.BodyClauses.Add (bodyClause);

      var newReferenceExpression = new QuerySourceReferenceExpression (ExpressionHelper.CreateMainFromClause_Int());
      _querySourceMapping.AddMapping (oldReferencedClause, newReferenceExpression);

      var clone = _queryModel.Clone (_querySourceMapping);
      Assert.That (((WhereClause) clone.BodyClauses[0]).Predicate, Is.SameAs (newReferenceExpression));
    }

    [Test]
    public void Clone_ResultOperators ()
    {
      var resultOperator1 = ExpressionHelper.CreateResultOperator ();
      _queryModel.ResultOperators.Add (resultOperator1);
      var resultOperator2 = ExpressionHelper.CreateResultOperator ();
      _queryModel.ResultOperators.Add (resultOperator2);

      var clone = _queryModel.Clone ();

      Assert.That (clone.ResultOperators.Count, Is.EqualTo (2));
      Assert.That (clone.ResultOperators[0], Is.Not.SameAs (resultOperator1));
      Assert.That (clone.ResultOperators[0].GetType (), Is.SameAs (resultOperator1.GetType ()));
      Assert.That (clone.ResultOperators[1], Is.Not.SameAs (resultOperator2));
      Assert.That (clone.ResultOperators[1].GetType (), Is.SameAs (resultOperator2.GetType ()));
    }

    [Test]
    public void Clone_ResultOperators_PassesMapping ()
    {
      var resultOperatorMock = MockRepository.GenerateMock<ResultOperatorBase> ();
      _queryModel.ResultOperators.Add (resultOperatorMock);

      resultOperatorMock
          .Expect (mock => mock.Clone (Arg<CloneContext>.Matches (cc => cc.QuerySourceMapping == _querySourceMapping)))
          .Return (ExpressionHelper.CreateResultOperator ());
      resultOperatorMock.Replay ();

      _queryModel.Clone (_querySourceMapping);

      resultOperatorMock.VerifyAllExpectations ();
    }


    [Test]
    public void TransformExpressions ()
    {
      Func<Expression, Expression> transformation = ex => ex;
      var fromClauseMock = MockRepository.GenerateMock<MainFromClause> ("item", typeof (string), ExpressionHelper.CreateExpression());
      var bodyClauseMock = MockRepository.GenerateMock<IBodyClause>();
      var selectClauseMock = MockRepository.GenerateMock<SelectClause> (ExpressionHelper.CreateExpression());

      var queryModel = new QueryModel (fromClauseMock, selectClauseMock);
      queryModel.BodyClauses.Add (bodyClauseMock);

      queryModel.TransformExpressions (transformation);

      fromClauseMock.AssertWasCalled (mock => mock.TransformExpressions (transformation));
      bodyClauseMock.AssertWasCalled (mock => mock.TransformExpressions (transformation));
      selectClauseMock.AssertWasCalled (mock => mock.TransformExpressions (transformation));
    }

    [Test]
    public void TransformExpressions_PassedToResultOperators ()
    {
      Func<Expression, Expression> transformer = ex => ex;
      var resultOperatorMock = MockRepository.GenerateMock<ResultOperatorBase> ();
      _queryModel.ResultOperators.Add (resultOperatorMock);
      resultOperatorMock.Expect (mock => mock.TransformExpressions (transformer));

      resultOperatorMock.Replay ();

      _queryModel.TransformExpressions (transformer);

      resultOperatorMock.VerifyAllExpectations ();
    }


    [Test]
    public void SelectOrGroupClause_Set ()
    {
      var newSelectClause = ExpressionHelper.CreateSelectClause();
      _queryModel.SelectClause = newSelectClause;

      Assert.That (_queryModel.SelectClause, Is.SameAs (newSelectClause));
    }

    [Test]
    [ExpectedException (typeof (ArgumentNullException))]
    public void SelectOrGroupClause_Set_Null ()
    {
      _queryModel.SelectClause = null;
    }

    [Test]
    public void GetNewName ()
    {
      var identifier = _queryModel.GetNewName ("test");
      Assert.That (identifier, Is.EqualTo ("test0"));
    }

    [Test]
    public void GetNewName_MoreThanOnce ()
    {
      var identifier1 = _queryModel.GetNewName ("test");
      var identifier2 = _queryModel.GetNewName ("test");
      var identifier3 = _queryModel.GetNewName ("test");
      Assert.That (identifier1, Is.Not.EqualTo (identifier2));
      Assert.That (identifier2, Is.Not.EqualTo (identifier3));
      Assert.That (identifier1, Is.Not.EqualTo (identifier3));
    }

    [Test]
    public void GetNewName_AlreadyExists_MainFromClause ()
    {
      var mainFromClause = new MainFromClause ("test0", typeof (Cook), ExpressionHelper.CreateQueryable<Cook>().Expression);
      var queryModel = ExpressionHelper.CreateQueryModel (mainFromClause);
      var identifier = queryModel.GetNewName ("test");
      Assert.That (identifier, Is.EqualTo ("test1"));
    }

    [Test]
    public void GetNewName_AlreadyExists_ChangedMainFromClause ()
    {
      var mainFromClause = new MainFromClause ("test0", typeof (Cook), ExpressionHelper.CreateQueryable<Cook>().Expression);
      var queryModel = ExpressionHelper.CreateQueryModel<Cook>();
      queryModel.MainFromClause = mainFromClause;
      var identifier = queryModel.GetNewName ("test");
      Assert.That (identifier, Is.EqualTo ("test1"));
    }

    [Test]
    public void GetNewName_AlreadyExists_BodyClauses ()
    {
      var additionalFromClause = new AdditionalFromClause (
          "test0",
          typeof (Cook),
          ExpressionHelper.CreateExpression());
      _queryModel.BodyClauses.Add (additionalFromClause);

      var identifier = _queryModel.GetNewName ("test");
      Assert.That (identifier, Is.EqualTo ("test1"));
    }

    [Test]
    public void GetNewName_AlreadyExists_ReplacedBodyClauses ()
    {
      _queryModel.BodyClauses.Add (ExpressionHelper.CreateAdditionalFromClause());

      var additionalFromClause = new AdditionalFromClause (
          "test0",
          typeof (Cook),
          ExpressionHelper.CreateExpression());
      _queryModel.BodyClauses[0] = additionalFromClause;

      var identifier = _queryModel.GetNewName ("test");
      Assert.That (identifier, Is.EqualTo ("test1"));
    }

    [Test]
    public void InitializeWithISelectOrGroupClauseAndOrderByClause ()
    {
      var orderByClause = new OrderByClause();
      _queryModel.BodyClauses.Add (orderByClause);

      var ordering = new Ordering (ExpressionHelper.CreateExpression(), OrderingDirection.Asc);
      orderByClause.Orderings.Add (ordering);

      Assert.That (_queryModel.SelectClause, Is.SameAs (_selectClause));
      Assert.That (_queryModel.BodyClauses.Count, Is.EqualTo (1));
      Assert.That (_queryModel.BodyClauses, Has.Member (orderByClause));
    }

    [Test]
    public void AddSeveralOrderByClauses ()
    {
      IBodyClause orderByClause1 = ExpressionHelper.CreateOrderByClause();
      IBodyClause orderByClause2 = ExpressionHelper.CreateOrderByClause();

      _queryModel.BodyClauses.Add (orderByClause1);
      _queryModel.BodyClauses.Add (orderByClause2);

      Assert.That (_queryModel.BodyClauses.Count, Is.EqualTo (2));
      Assert.That (_queryModel.BodyClauses, Is.EqualTo (new object[] { orderByClause1, orderByClause2 }));
    }

    [Test]
    public void AddBodyClause ()
    {
      IBodyClause clause = ExpressionHelper.CreateWhereClause();
      _queryModel.BodyClauses.Add (clause);

      Assert.That (_queryModel.BodyClauses.Count, Is.EqualTo (1));
      Assert.That (_queryModel.BodyClauses, Has.Member (clause));
    }

    [Test]
    [ExpectedException (typeof (ArgumentNullException))]
    public void AddBodyClause_Null ()
    {
      _queryModel.BodyClauses.Add (null);
    }

    [Test]
    [ExpectedException (typeof (ArgumentNullException))]
    public void SetBodyClause_Null ()
    {
      _queryModel.BodyClauses.Add (ExpressionHelper.CreateWhereClause());
      _queryModel.BodyClauses[0] = null;
    }

    [Test]
    [ExpectedException (typeof (ArgumentNullException))]
    public void AddResultOperator_Null_ThrowsArgumentNullException ()
    {
      _queryModel.ResultOperators.Add (null);
    }

    [Test]
    [ExpectedException (typeof (ArgumentNullException))]
    public void SetResultOperator_Null_ThrowsArgumentNullException ()
    {
      var resultOperator = new DistinctResultOperator ();
      _queryModel.ResultOperators.Add (resultOperator);
      _queryModel.ResultOperators[0] = null;
    }

    [Test]
    public void Execute ()
    {
      var executorMock = MockRepository.GenerateMock<IQueryExecutor>();
      var mockResult = new[] { 0, 0, 0 };
      executorMock.Expect (mock => mock.ExecuteCollection<int> (_queryModel)).Return (mockResult);

      var result = (StreamedSequence) _queryModel.Execute (executorMock);

      executorMock.VerifyAllExpectations();
      Assert.That (result.GetTypedSequence<int>().ToArray(), Is.EqualTo (mockResult));
    }

    [Test]
    public void IsIdentityQuery_True ()
    {
      var queryModel = new QueryModel (_mainFromClause, new SelectClause (new QuerySourceReferenceExpression (_mainFromClause)));
      Assert.That (queryModel.IsIdentityQuery (), Is.True);
    }

    [Test]
    public void IsIdentityQuery_True_WithResultOperator ()
    {
      var queryModel = new QueryModel (_mainFromClause, new SelectClause (new QuerySourceReferenceExpression (_mainFromClause)));
      queryModel.ResultOperators.Add (new DistinctResultOperator ());
      Assert.That (queryModel.IsIdentityQuery (), Is.True);
    }

    [Test]
    public void IsIdentityQuery_False_BodyClause ()
    {
      var queryModel = new QueryModel (_mainFromClause, new SelectClause (new QuerySourceReferenceExpression (_mainFromClause)));
      queryModel.BodyClauses.Add (new WhereClause (Expression.Constant (false)));
      Assert.That (queryModel.IsIdentityQuery (), Is.False);
    }

    [Test]
    public void IsIdentityQuery_False_Selector_NonReference ()
    {
      var queryModel = new QueryModel (_mainFromClause, new SelectClause (Expression.Constant(0)));
      Assert.That (queryModel.IsIdentityQuery (), Is.False);
    }

    [Test]
    public void IsIdentityQuery_False_Selector_WrongReference ()
    {
      var queryModel = new QueryModel (_mainFromClause, new SelectClause (new QuerySourceReferenceExpression (ExpressionHelper.CreateMainFromClause_Int())));
      Assert.That (queryModel.IsIdentityQuery (), Is.False);
    }

    [Test]
    public void ConvertToSubquery ()
    {
      var queryModel = new QueryModel (_mainFromClause, new SelectClause (new QuerySourceReferenceExpression (_mainFromClause)));

      var result = queryModel.ConvertToSubQuery ("test");

      Assert.That (result.MainFromClause.ItemName, Is.EqualTo ("test"));
      Assert.That (result.MainFromClause.ItemType, Is.SameAs(typeof(int)));
      Assert.That (result.MainFromClause.FromExpression, Is.TypeOf(typeof (SubQueryExpression)));
      Assert.That (((SubQueryExpression) result.MainFromClause.FromExpression).QueryModel, Is.SameAs(queryModel));
      Assert.That (result.SelectClause.Selector, Is.TypeOf(typeof(QuerySourceReferenceExpression)));
      Assert.That (((QuerySourceReferenceExpression) result.SelectClause.Selector).ReferencedQuerySource, Is.SameAs (result.MainFromClause));
    }

    [Test]
    public void ConvertToSubquery_CovariantSubquery ()
    {
      var queryModel = new QueryModel (_mainFromClause, new SelectClause (new QuerySourceReferenceExpression (_mainFromClause)));
      queryModel.ResultTypeOverride = typeof (IEnumerable<object>);

      var result = queryModel.ConvertToSubQuery ("test");

      Assert.That (result.MainFromClause.ItemName, Is.EqualTo ("test"));
      Assert.That (result.MainFromClause.ItemType, Is.SameAs (typeof (object)));
      Assert.That (result.MainFromClause.FromExpression, Is.TypeOf (typeof (SubQueryExpression)));
      Assert.That (((SubQueryExpression) result.MainFromClause.FromExpression).QueryModel, Is.SameAs (queryModel));
      Assert.That (result.SelectClause.Selector.Type, Is.SameAs (typeof (object)));
    }

    [Test]
    [ExpectedException (typeof (InvalidOperationException), ExpectedMessage = 
      "The query must return a sequence of items, but it selects a single object of type 'System.Int32'.")]
    public void ConvertToSubquery_NoStreamedSequenceInfo ()
    {
      var queryModel = new QueryModel (_mainFromClause, new SelectClause (new QuerySourceReferenceExpression (_mainFromClause)));
      queryModel.ResultOperators.Add (new CountResultOperator());

      queryModel.ConvertToSubQuery ("test");
    }
  }
}
