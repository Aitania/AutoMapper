namespace AutoMapper.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Execution;

    public interface IMemberConfiguration
    {
        void Configure(TypeMap typeMap);
        IMemberAccessor DestinationMember { get; }
    }

    public class MemberConfigurationExpression<TSource, TMember> : IMemberConfigurationExpression<TSource, TMember>, IMemberConfiguration
    {
        private readonly IMemberAccessor _destinationMember;
        private readonly Type _sourceType;
        private readonly List<Action<PropertyMap>> _propertyMapActions = new List<Action<PropertyMap>>();

        public MemberConfigurationExpression(IMemberAccessor destinationMember, Type sourceType)
        {
            _destinationMember = destinationMember;
            _sourceType = sourceType;
        }

        public IMemberAccessor DestinationMember => _destinationMember;

        public void NullSubstitute(object nullSubstitute)
        {
            _propertyMapActions.Add(pm => pm.SetNullSubstitute(nullSubstitute));
        }

        public IResolverConfigurationExpression<TSource, TValueResolver> ResolveUsing<TValueResolver>() where TValueResolver : IValueResolver
        {
            var resolver = new DeferredInstantiatedResolver(typeof(TValueResolver).BuildCtor<IValueResolver>());

            ResolveUsing(resolver);

            var expression = new ResolutionExpression<TSource, TValueResolver>(_sourceType);

            _propertyMapActions.Add(pm => expression.Configure(pm));

            return expression;
        }

        public IResolverConfigurationExpression<TSource> ResolveUsing(Type valueResolverType)
        {
            var resolver = new DeferredInstantiatedResolver(valueResolverType.BuildCtor<IValueResolver>());

            ResolveUsing(resolver);

            var expression = new ResolutionExpression<TSource>(_sourceType);

            _propertyMapActions.Add(pm => expression.Configure(pm));

            return expression;
        }

        public IResolutionExpression<TSource> ResolveUsing(IValueResolver valueResolver)
        {
            _propertyMapActions.Add(pm => pm.AssignCustomValueResolver(valueResolver));

            var expression = new ResolutionExpression<TSource>(_sourceType);

            _propertyMapActions.Add(pm => expression.Configure(pm));

            return expression;
        }

        public void ResolveUsing<TSourceMember>(Func<TSource, TSourceMember> resolver)
        {
            _propertyMapActions.Add(pm => pm.AssignCustomValueResolver(new DelegateBasedResolver<TSource, TSourceMember>((o, c) => resolver((TSource)o))));
        }

        public void ResolveUsing<TSourceMember>(Func<TSource, ResolutionContext, TSourceMember> resolver)
        {
            _propertyMapActions.Add(pm => pm.AssignCustomValueResolver(new DelegateBasedResolver<TSource, TSourceMember>((o, c) => resolver((TSource)o, c))));
        }

        public void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        {
            _propertyMapActions.Add(pm => pm.SetCustomValueResolverExpression(sourceMember));
        }

        public void MapFrom<TSourceMember>(string sourceMember)
        {
            var members = _sourceType.GetMember(sourceMember);
            if (!members.Any())
                throw new AutoMapperConfigurationException($"Unable to find source member {sourceMember} on type {_sourceType.FullName}");
            if (members.Skip(1).Any())
                throw new AutoMapperConfigurationException($"Source member {sourceMember} is ambiguous on type {_sourceType.FullName}");
            var member = members.Single();

            var par = Expression.Parameter(typeof(TSource));
            var prop = typeof(TSource) != _sourceType ? (Expression) Expression.Property(Expression.Convert(par, _sourceType), sourceMember) : Expression.Property(par, sourceMember);
            if (typeof (TSourceMember) != member.GetMemberType())
                prop = Expression.Convert(prop, typeof(TSourceMember));

            var lambda = Expression.Lambda<Func<TSource, TSourceMember>>(prop, par);

            _propertyMapActions.Add(pm => pm.SetCustomValueResolverExpression(lambda));
        }

        public void MapFrom(string sourceMember)
        {
            MapFrom<object>(sourceMember);
        }

        public void UseValue<TValue>(TValue value)
        {
            MapFrom(src => value);
        }

        public void UseValue(object value)
        {
            _propertyMapActions.Add(pm => pm.AssignCustomValueResolver(new DelegateBasedResolver<TSource, object>((o, c) => value)));
        }

        public void Condition(Func<TSource, object, TMember, ResolutionContext, bool> condition)
        {
            _propertyMapActions.Add(pm => pm.ApplyCondition((src, dest, ctxt) => condition((TSource)ctxt.SourceValue, src, (TMember)dest, ctxt)));
        }

        public void Condition(Func<TSource, object, TMember, bool> condition)
        {
            _propertyMapActions.Add(pm => pm.ApplyCondition((src, dest, ctxt) => condition((TSource)ctxt.SourceValue, src, (TMember)dest)));
        }

        public void Condition(Func<TSource, bool> condition)
        {
            _propertyMapActions.Add(pm => pm.ApplyCondition((src, dest, ctxt) => condition((TSource)ctxt.SourceValue)));
        }

        public void PreCondition(Func<TSource, bool> condition)
        {
            PreCondition(context => condition((TSource)context.SourceValue));
        }

        public void PreCondition(Func<ResolutionContext, bool> condition)
        {
            _propertyMapActions.Add(pm => pm.ApplyPreCondition(condition));
        }

        public void ExplicitExpansion()
        {
            _propertyMapActions.Add(pm => pm.ExplicitExpansion = true);
        }

        public void Ignore()
        {
            _propertyMapActions.Add(pm => pm.Ignore());
        }

        public void UseDestinationValue()
        {
            _propertyMapActions.Add(pm => pm.UseDestinationValue = true);
        }

        public void DoNotUseDestinationValue()
        {
            _propertyMapActions.Add(pm => pm.UseDestinationValue = false);
        }

        public void SetMappingOrder(int mappingOrder)
        {
            _propertyMapActions.Add(pm => pm.SetMappingOrder(mappingOrder));
        }

        public void Configure(TypeMap typeMap)
        {
            var propertyMap = typeMap.FindOrCreatePropertyMapFor(_destinationMember);

            foreach (var action in _propertyMapActions)
            {
                action(propertyMap);
            }
        }
    }
}