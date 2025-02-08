using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Framework.Utils;
using FastExpressionCompiler;

namespace DotVVM.Framework.Controls;

/// <summary> Creates data bindings for GridView, DataPager and related components. </summary>
public class GridViewDataSetBindingProvider
{
    private readonly BindingCompilationService service;

    private readonly ConcurrentDictionary<(DataContextStack dataContextStack, IStaticValueBinding dataSetBinding, GridViewDataSetCommandType commandType), DataPagerBindings> dataPagerCommands = new();
    private readonly ConcurrentDictionary<(DataContextStack dataContextStack, IStaticValueBinding dataSetBinding, GridViewDataSetCommandType commandType), GridViewBindings> gridViewCommands = new();

    public GridViewDataSetBindingProvider(BindingCompilationService service)
    {
        this.service = service;
    }

    /// <summary> Returns pre-created DataPager bindings for a given data context and data source (result is cached). </summary>
    public DataPagerBindings GetDataPagerBindings(DataContextStack dataContextStack, IStaticValueBinding dataSetBinding, GridViewDataSetCommandType commandType)
    {
        return dataPagerCommands.GetOrAdd((dataContextStack, dataSetBinding, commandType), x => GetDataPagerCommandsCore(x.dataContextStack, x.dataSetBinding, x.commandType));
    }

    /// <summary> Returns pre-created GridView bindings for a given data context and data source (result is cached). </summary>
    public GridViewBindings GetGridViewBindings(DataContextStack dataContextStack, IStaticValueBinding dataSetBinding, GridViewDataSetCommandType commandType)
    {
        return gridViewCommands.GetOrAdd((dataContextStack, dataSetBinding, commandType), x => GetGridViewBindingsCore(x.dataContextStack, x.dataSetBinding, x.commandType));
    }

    private DataPagerBindings GetDataPagerCommandsCore(DataContextStack dataContextStack, IStaticValueBinding dataSetBinding, GridViewDataSetCommandType commandType)
    {
        var isServerOnly = dataSetBinding is not IValueBinding;
        var dataSetExpr = dataSetBinding.GetProperty<ParsedExpressionBindingProperty>().Expression;
        ICommandBinding? GetCommandOrNull<T>(DataContextStack dataContextStack, string methodName, params Expression[] arguments)
        {
            return typeof(T).IsAssignableFrom(dataSetExpr.Type)
                ? CreateCommandBinding<T>(commandType, dataSetExpr, dataContextStack, methodName, arguments)
                : null;
        }

        IStaticValueBinding<TResult>? GetValueBindingOrNull<T, TResult>(Expression<Func<T, TResult>> expression)
        {
            if (typeof(T).IsAssignableFrom(dataSetExpr.Type))
            {
                return (IStaticValueBinding<TResult>)ValueOrBindingExtensions.SelectImpl(dataSetBinding, expression);
            }
            else
            {
                return null;
            }
        }

        ParameterExpression CreateParameter(DataContextStack dataContextStack, string name = "_this")
        {
            return Expression.Parameter(dataContextStack.DataContextType, name).AddParameterAnnotation(new BindingParameterAnnotation(dataContextStack));
        }

        var pageIndexDataContext = DataContextStack.CreateCollectionElement(
            typeof(int), dataContextStack, serverSideOnly: isServerOnly
        );

        var isFirstPage = GetValueBindingOrNull<IPageableGridViewDataSet<IPagingFirstPageCapability>, bool>(d => d.PagingOptions.IsFirstPage) ??
                GetValueBindingOrNull<IPageableGridViewDataSet<IPagingPreviousPageCapability>, bool>(d => d.PagingOptions.IsFirstPage);
        var isLastPage = GetValueBindingOrNull<IPageableGridViewDataSet<IPagingLastPageCapability>, bool>(d => d.PagingOptions.IsLastPage) ??
                GetValueBindingOrNull<IPageableGridViewDataSet<IPagingNextPageCapability>, bool>(d => d.PagingOptions.IsLastPage);

        return new DataPagerBindings()
        {
            GoToFirstPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingFirstPageCapability>>(
                dataContextStack,
                nameof(IPagingFirstPageCapability.GoToFirstPage)),

            GoToPreviousPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingPreviousPageCapability>>(
                dataContextStack,
                nameof(IPagingPreviousPageCapability.GoToPreviousPage)),

            GoToNextPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingNextPageCapability>>(
                dataContextStack,
                nameof(IPagingNextPageCapability.GoToNextPage)),

            GoToLastPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingLastPageCapability>>(
                dataContextStack,
                nameof(IPagingLastPageCapability.GoToLastPage)),

            GoToPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingPageIndexCapability>>(
                pageIndexDataContext,
                nameof(IPagingPageIndexCapability.GoToPage),
                CreateParameter(pageIndexDataContext, "_thisIndex")),

            IsFirstPage = isFirstPage,
            IsLastPage = isLastPage,
            PageNumbers =
                GetValueBindingOrNull<IPageableGridViewDataSet<IPagingPageIndexCapability>, IEnumerable<int>>(d => d.PagingOptions.NearPageIndexes),
            
            IsActivePage = // _this == _parent.DataSet.PagingOptions.PageIndex
                typeof(IPageableGridViewDataSet<IPagingPageIndexCapability>).IsAssignableFrom(dataSetExpr.Type)
                    ? ValueOrResourceBinding<bool>(isServerOnly, [
                        pageIndexDataContext,
                        new ParsedExpressionBindingProperty(Expression.Equal(
                            CreateParameter(pageIndexDataContext, "_thisIndex"),
                            Expression.Property(Expression.Property(dataSetExpr, "PagingOptions"), "PageIndex")
                        )),
                    ])
                    : null,

            PageNumberText = isServerOnly switch {
                true => service.Cache.CreateResourceBinding<string>("(_this + 1) + ''", pageIndexDataContext),
                false => service.Cache.CreateValueBinding<string>("(_this + 1) + ''", pageIndexDataContext)
            },
            HasMoreThanOnePage =
                GetValueBindingOrNull<IPageableGridViewDataSet<PagingOptions>, bool>(d => d.PagingOptions.PagesCount > 1) ??
                (isFirstPage != null && isLastPage != null ?
                    ValueOrResourceBinding<bool>(isServerOnly, [
                        dataContextStack,
                        new ParsedExpressionBindingProperty(Expression.Not(Expression.AndAlso(
                            isFirstPage.GetProperty<ParsedExpressionBindingProperty>().Expression,
                            isLastPage.GetProperty<ParsedExpressionBindingProperty>().Expression
                        )))
                    ]) : null)
        };
    }

    private GridViewBindings GetGridViewBindingsCore(DataContextStack dataContextStack, IStaticValueBinding dataSetBinding, GridViewDataSetCommandType commandType)
    {
        var isServerOnly = dataSetBinding is not IValueBinding;
        var dataSetExpr = dataSetBinding.GetProperty<ParsedExpressionBindingProperty>().Expression;
        ICommandBinding? GetCommandOrNull<T>(DataContextStack dataContextStack, string methodName, Expression[] arguments, Func<Expression, Expression>? transformExpression)
        {
            return typeof(T).IsAssignableFrom(dataSetExpr.Type)
                ? CreateCommandBinding<T>(commandType, dataSetExpr, dataContextStack, methodName, arguments, transformExpression)
                : null;
        }
        IStaticValueBinding<TResult>? GetValueBindingOrNull<T, TResult>(DataContextStack dataContextStack, string methodName, Expression[] arguments, Func<Expression, Expression>? transformExpression)
        {
            return typeof(T).IsAssignableFrom(dataSetExpr.Type)
                ? CreateValueBinding<T, TResult>(dataSetExpr, dataContextStack, methodName, arguments, transformExpression, isServerOnly)
                : null;
        }

        var setSortExpressionParam = Expression.Parameter(typeof(string), "_sortExpression");
        return new GridViewBindings()
        {
            SetSortExpression = GetCommandOrNull<ISortableGridViewDataSet<ISortingSetSortExpressionCapability>>(
                dataContextStack,
                nameof(ISortingSetSortExpressionCapability.SetSortExpression),
                new Expression[] { setSortExpressionParam },
                // transform to sortExpression => command lambda
                e => Expression.Lambda(e, setSortExpressionParam)),
            IsColumnSortedAscending = GetValueBindingOrNull<ISortableGridViewDataSet<ISortingStateCapability>, Func<string, bool>>(
                dataContextStack,
                nameof(ISortingStateCapability.IsColumnSortedAscending),
                new Expression[] { setSortExpressionParam },
                // transform to sortExpression => command lambda
                e => Expression.Lambda(e, setSortExpressionParam)),
            IsColumnSortedDescending = GetValueBindingOrNull<ISortableGridViewDataSet<ISortingStateCapability>, Func<string, bool>>(
                dataContextStack,
                nameof(ISortingStateCapability.IsColumnSortedDescending),
                new Expression[] { setSortExpressionParam },
                // transform to sortExpression => command lambda
                e => Expression.Lambda(e, setSortExpressionParam)),
        };
    }

    private IStaticValueBinding<TResult> CreateValueBinding<TDataSetInterface, TResult>(Expression dataSet, DataContextStack dataContextStack, string methodName, Expression[] arguments, Func<Expression, Expression>? transformExpression = null, bool isServerOnly = false)
    {
        // get concrete type from implementation of IXXXableGridViewDataSet<?>
        var optionsConcreteType = GetOptionsConcreteType<TDataSetInterface>(dataSet.Type, out var optionsProperty);

        // call dataSet.XXXOptions.Method(...);
        Expression expression = Expression.Call(
            Expression.Convert(Expression.Property(dataSet, optionsProperty), optionsConcreteType),
            optionsConcreteType.GetMethod(methodName)!,
            arguments);

        if (transformExpression != null)
        {
            expression = transformExpression(expression);
        }

        return ValueOrResourceBinding<TResult>(isServerOnly,
                    [
                        new ParsedExpressionBindingProperty(expression),
                        dataContextStack
                    ]);
    }

    private IStaticValueBinding<T> ValueOrResourceBinding<T>(bool isServerOnly, object?[] properties)
    {
        if (isServerOnly)
            return new ResourceBindingExpression<T>(service, properties);
        else
            return new ValueBindingExpression<T>(service, properties);
    }

    private ICommandBinding CreateCommandBinding<TDataSetInterface>(GridViewDataSetCommandType commandType, Expression dataSet, DataContextStack dataContextStack, string methodName, Expression[] arguments, Func<Expression, Expression>? transformExpression = null)
    {
        if (commandType == GridViewDataSetCommandType.Default)
        {
            // create a binding {command: dataSet.XXXOptions.YYY(args); dataSet.RequestRefresh() }
            return CreateDefaultCommandBinding<TDataSetInterface>(dataSet, dataContextStack, methodName, arguments, transformExpression);
        }
        else if (commandType == GridViewDataSetCommandType.LoadDataDelegate)
        {
            // create a binding {staticCommand: GridViewDataSetBindingProvider.LoadDataSet(dataSet, options => { options.XXXOptions.YYY(args); }, loadDataDelegate, postProcessor) }
            return CreateLoadDataDelegateCommandBinding<TDataSetInterface>(dataSet, dataContextStack, methodName, arguments, transformExpression);
        }
        else
        {
            throw new NotSupportedException($"The command type {commandType} is not supported!");
        }
    }

    private ICommandBinding CreateDefaultCommandBinding<TDataSetInterface>(Expression dataSet, DataContextStack dataContextStack, string methodName, Expression[] arguments, Func<Expression, Expression>? transformExpression)
    {
        var body = new List<Expression>();

        // get concrete type from implementation of IXXXableGridViewDataSet<?>
        var optionsConcreteType = GetOptionsConcreteType<TDataSetInterface>(dataSet.Type, out var optionsProperty);

        // call dataSet.XXXOptions.Method(...);
        var callMethodOnOptions = Expression.Call(
            Expression.Convert(Expression.Property(dataSet, optionsProperty), optionsConcreteType),
            optionsConcreteType.GetMethod(methodName)!,
            arguments);
        body.Add(callMethodOnOptions);

        // if we are on a server, call the dataSet.RequestRefresh if supported
        if (typeof(IRefreshableGridViewDataSet).IsAssignableFrom(dataSet.Type))
        {
            var callRequestRefresh = Expression.Call(
                Expression.Convert(dataSet, typeof(IRefreshableGridViewDataSet)),
                typeof(IRefreshableGridViewDataSet).GetMethod(nameof(IRefreshableGridViewDataSet.RequestRefresh))!
            );
            body.Add(callRequestRefresh);
        }

        // build command binding
        Expression expression = Expression.Block(body);
        if (transformExpression != null)
        {
            expression = transformExpression(expression);
        }

        return new CommandBindingExpression(service,
            new object[]
            {
                new ParsedExpressionBindingProperty(expression),
                new OriginalStringBindingProperty($"DataPager({dataSet.Type.ToCode()}): {dataSet.ToCSharpString().TrimEnd(';')}.{methodName}({string.Join(", ", arguments.AsEnumerable())})"), // For ID generation
                dataContextStack
            });
    }

    private ICommandBinding CreateLoadDataDelegateCommandBinding<TDataSetInterface>(Expression dataSet, DataContextStack dataContextStack, string methodName, Expression[] arguments, Func<Expression, Expression>? transformExpression)
    {
        var loadDataSetMethod = typeof(GridViewDataSetBindingProvider).GetMethod(nameof(DataSetClientSideLoad))!;

        // build the concrete type of GridViewDataSetOptions<,,>
        GetOptionsConcreteType<IFilterableGridViewDataSet>(dataSet.Type, out var filteringOptionsProperty);
        GetOptionsConcreteType<ISortableGridViewDataSet>(dataSet.Type, out var sortingOptionsProperty);
        GetOptionsConcreteType<IPageableGridViewDataSet>(dataSet.Type, out var pagingOptionsProperty);
        GetOptionsConcreteType<IBaseGridViewDataSet>(dataSet.Type, out var itemProperty);
        var itemType = itemProperty.PropertyType.GetEnumerableType()!;

        var optionsType = typeof(GridViewDataSetOptions<,,>).MakeGenericType(filteringOptionsProperty.PropertyType, sortingOptionsProperty.PropertyType, pagingOptionsProperty.PropertyType);
        var resultType = typeof(GridViewDataSetResult<,,,>).MakeGenericType(itemType, filteringOptionsProperty.PropertyType, sortingOptionsProperty.PropertyType, pagingOptionsProperty.PropertyType);

        // get concrete type from implementation of IXXXableGridViewDataSet<?>
        var modifiedOptionsType = GetOptionsConcreteType<TDataSetInterface>(dataSet.Type, out var modifiedOptionsProperty);

        // call options.XXXOptions.Method(...);
        var optionsParameter = Expression.Parameter(optionsType, "options");
        var callMethodOnOptions = Expression.Call(
                Expression.Convert(Expression.Property(optionsParameter, modifiedOptionsProperty.Name), modifiedOptionsType),
                modifiedOptionsType.GetMethod(methodName)!,
                arguments);

        // build options => options.XXXOptions.Method(...)
        var optionsTransformLambdaType = typeof(Action<>).MakeGenericType(optionsType);
        var optionsTransformLambda = Expression.Lambda(optionsTransformLambdaType, callMethodOnOptions, optionsParameter);

        var expression = (Expression)Expression.Call(
            loadDataSetMethod.MakeGenericMethod(dataSet.Type, itemType, filteringOptionsProperty.PropertyType, sortingOptionsProperty.PropertyType, pagingOptionsProperty.PropertyType),
            dataSet,
            optionsTransformLambda,
            Expression.Constant(null, typeof(Func<,>).MakeGenericType(optionsType, typeof(Task<>).MakeGenericType(resultType))),
            Expression.Constant(null, typeof(Action<,>).MakeGenericType(dataSet.Type, resultType)));

        if (transformExpression != null)
        {
            expression = transformExpression(expression);
        }
        return new StaticCommandBindingExpression(service,
            new object[]
            {
                new ParsedExpressionBindingProperty(expression),
                BindingParserOptions.StaticCommand,
                dataContextStack
            });
    }

    public static CodeSymbolicParameter LoadDataDelegate = new CodeSymbolicParameter(
        "LoadDataDelegate",
        CodeParameterAssignment.FromExpression(JavascriptTranslator.KnockoutContextParameter.ToExpression().Member("$gridViewDataSetHelper").Member("loadDataSet"))
    );
    public static CodeSymbolicParameter PostProcessorDelegate = new CodeSymbolicParameter(
        "PostProcessorDelegate",
        CodeParameterAssignment.FromExpression(JavascriptTranslator.KnockoutContextParameter.ToExpression().Member("$gridViewDataSetHelper").Member("postProcessor"))
    );

    /// <summary>
    /// A sentinel method which is translated to load the GridViewDataSet on the client side using the Load delegate.
    /// Do not call this method on the server.
    /// </summary>
    public static Task DataSetClientSideLoad<TGridViewDataSet, TItem, TFilteringOptions, TSortingOptions, TPagingOptions>(
        IBaseGridViewDataSet dataSet,
        Action<GridViewDataSetOptions<TFilteringOptions, TSortingOptions, TPagingOptions>> optionsTransformer,
        Func<GridViewDataSetOptions<TFilteringOptions, TSortingOptions, TPagingOptions>, Task<GridViewDataSetResult<TItem, TFilteringOptions, TSortingOptions, TPagingOptions>>> loadDataDelegate,
        Action<TGridViewDataSet, GridViewDataSetResult<TItem, TFilteringOptions, TSortingOptions, TPagingOptions>> postProcessor)
        where TFilteringOptions : IFilteringOptions
        where TSortingOptions : ISortingOptions
        where TPagingOptions : IPagingOptions
    {
        throw new InvalidOperationException("This method cannot be called on the server!");
    }

    private static Type GetOptionsConcreteType<TDataSetInterface>(Type dataSetConcreteType, out PropertyInfo optionsProperty)
    {
        if (!typeof(TDataSetInterface).IsAssignableFrom(dataSetConcreteType))
        {
            throw new ArgumentException($"The type {typeof(TDataSetInterface)} must be a generic type and must be implemented by the type {dataSetConcreteType} specified in {nameof(dataSetConcreteType)} argument!");
        }

        // resolve options property
        var genericInterface = typeof(TDataSetInterface).IsGenericType ? typeof(TDataSetInterface).GetGenericTypeDefinition() : typeof(TDataSetInterface);
        if (genericInterface == typeof(IFilterableGridViewDataSet<>) || genericInterface == typeof(IFilterableGridViewDataSet))
        {
            optionsProperty = typeof(TDataSetInterface).GetProperty(nameof(IFilterableGridViewDataSet.FilteringOptions))!;
            genericInterface = typeof(IFilterableGridViewDataSet<>);
        }
        else if (genericInterface == typeof(ISortableGridViewDataSet<>) || genericInterface == typeof(ISortableGridViewDataSet))
        {
            optionsProperty = typeof(TDataSetInterface).GetProperty(nameof(ISortableGridViewDataSet.SortingOptions))!;
            genericInterface = typeof(ISortableGridViewDataSet<>);
        }
        else if (genericInterface == typeof(IPageableGridViewDataSet<>) || genericInterface == typeof(IPageableGridViewDataSet))
        {
            optionsProperty = typeof(TDataSetInterface).GetProperty(nameof(IPageableGridViewDataSet.PagingOptions))!;
            genericInterface = typeof(IPageableGridViewDataSet<>);
        }
        else if (genericInterface == typeof(IBaseGridViewDataSet<>) || genericInterface == typeof(IBaseGridViewDataSet))
        {
            optionsProperty = typeof(TDataSetInterface).GetProperty(nameof(IBaseGridViewDataSet.Items))!;
            genericInterface = typeof(IBaseGridViewDataSet<>);
        }
        else
        {
            throw new ArgumentException($"The {typeof(TDataSetInterface)} can only be {nameof(IFilterableGridViewDataSet)}, {nameof(ISortableGridViewDataSet)} or {nameof(IPageableGridViewDataSet)} with one generic argument!");
        }

        var interfaces = dataSetConcreteType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface)
            .Distinct()
            .ToList();
        if (interfaces.Count < 1)
        {
            throw new ArgumentException($"The {dataSetConcreteType} doesn't implement {genericInterface.Name}<TOptions>.");
        }
        else if (interfaces.Count > 1)
        {
            throw new ArgumentException($"The {dataSetConcreteType} implements multiple interfaces where {genericInterface.Name}<TOptions> ({interfaces.Select(i => i.ToCode()).StringJoin(", ")}). Only one implementation is allowed.");
        }

        var pagingOptionsConcreteType = interfaces[0].GetGenericArguments()[0];
        return pagingOptionsConcreteType;
    }
}
