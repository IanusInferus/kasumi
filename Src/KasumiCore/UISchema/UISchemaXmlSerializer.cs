using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using Firefly;
using Firefly.Mapping;
using Firefly.Mapping.MetaProgramming;
using Firefly.Mapping.XmlText;
using Firefly.Texting;

namespace Kasumi.UISchema
{
    /// <summary>
    /// 不同于Firefly的XmlSerializer的几点
    /// 
    /// 1)支持Record通过Attribute表示Field的属性
    /// 2)支持Record的Optional字段不写
    /// 3)支持Record的内容直接表示Content的字段，若Content的内容为空，且Context的类型为String，则返回空字符串而非空引用
    /// 4)支持TaggedUnion直接不写外层TaggedUnion名称
    /// </summary>
    public class UISchemaXmlSerializer : IXmlSerializer
    {
        private UISchemaXmlReaderResolver ReaderResolver;
        private UISchemaXmlWriterResolver WriterResolver;

        private IMapperResolver ReaderCache;
        private IMapperResolver WriterCache;

        public UISchemaXmlSerializer()
        {
            ReferenceMapperResolver ReaderReference = new ReferenceMapperResolver();
            ReaderCache = ReaderReference;
            ReaderResolver = new UISchemaXmlReaderResolver(ReaderReference);
            ReaderReference.Inner = ReaderResolver.AsCached();

            ReferenceMapperResolver WriterReference = new ReferenceMapperResolver();
            WriterCache = WriterReference;
            WriterResolver = new UISchemaXmlWriterResolver(WriterReference);
            WriterReference.Inner = WriterResolver.AsCached();

            ByteArrayTranslator bat = new ByteArrayTranslator();
            PutReaderTranslator(bat);
            PutWriterTranslator(bat);
            ByteListTranslator blt = new ByteListTranslator();
            PutReaderTranslator(blt);
            PutWriterTranslator(blt);
        }

        public void PutReader<T>(Func<String, T> Reader)
        {
            ReaderResolver.PutReader(Reader);
        }
        public void PutWriter<T>(Func<T, String> Writer)
        {
            WriterResolver.PutWriter(Writer);
        }
        public void PutReader<T>(Func<XElement, T> Reader)
        {
            ReaderResolver.PutReader(Reader);
        }
        public void PutWriter<T>(Func<T, XElement> Writer)
        {
            WriterResolver.PutWriter(Writer);
        }
        public void PutReaderTranslator<R, M>(IProjectorToProjectorRangeTranslator<R, M> Translator)
        {
            ReaderResolver.PutReaderTranslator(Translator);
        }
        public void PutWriterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            WriterResolver.PutWriterTranslator(Translator);
        }
        public void PutReaderTranslator<M>(IProjectorToProjectorDomainTranslator<XElement, M> Translator)
        {
            ReaderResolver.PutReaderTranslator(Translator);
        }
        public void PutWriterTranslator<M>(IProjectorToProjectorRangeTranslator<XElement, M> Translator)
        {
            WriterResolver.PutWriterTranslator(Translator);
        }

        public T Read<T>(XElement s)
        {
            var m = ReaderCache.ResolveProjector<XElement, T>();
            return m(s);
        }
        public XElement Write<T>(T Value)
        {
            var m = WriterCache.ResolveProjector<T, XElement>();
            return m(Value);
        }

        public XElement CurrentReadingXElement
        {
            get { return ReaderResolver.CurrentReadingXElement; }
        }
    }

    public class UISchemaXmlReaderResolver : IMapperResolver
    {
        private IMapperResolver Root;
        private PrimitiveResolver PrimitiveResolver;
        private IMapperResolver Resolver;
        private LinkedList<IProjectorResolver> ProjectorResolverList;

        private DebugReaderResolver DebugResolver;
        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveProjector(TypePair);
        }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveAggregator(TypePair);
        }

        public UISchemaXmlReaderResolver(IMapperResolver Root)
        {
            this.Root = Root;

            PrimitiveResolver = new PrimitiveResolver();

            PutReader((String s) => NumericStrings.InvariantParseUInt8(s));
            PutReader((String s) => NumericStrings.InvariantParseUInt16(s));
            PutReader((String s) => NumericStrings.InvariantParseUInt32(s));
            PutReader((String s) => NumericStrings.InvariantParseUInt64(s));
            PutReader((String s) => NumericStrings.InvariantParseInt8(s));
            PutReader((String s) => NumericStrings.InvariantParseInt16(s));
            PutReader((String s) => NumericStrings.InvariantParseInt32(s));
            PutReader((String s) => NumericStrings.InvariantParseInt64(s));
            PutReader((String s) => NumericStrings.InvariantParseFloat32(s));
            PutReader((String s) => NumericStrings.InvariantParseFloat64(s));
            PutReader((String s) => NumericStrings.InvariantParseBoolean(s));
            PutReader((String s) => s);
            PutReader((String s) => NumericStrings.InvariantParseDecimal(s));

            //Reader
            //proj <- proj
            //PrimitiveResolver: (String|XElement proj Primitive) <- null
            //EnumResolver: (String proj Enum) <- null
            //XElementToStringDomainTranslator: (XElement proj R) <- (String proj R)
            //CollectionUnpacker: (XElement proj {R}) <- (XElement.SubElement proj R)
            //FieldOrPropertyProjectorResolver: (Dictionary(String, XElement) proj R) <- (XElement.SubElement proj R.Field)
            //XElementProjectorToProjectorDomainTranslator: (XElement proj R) <- (Dictionary(String, XElement) proj R)

            ProjectorResolverList = new LinkedList<IProjectorResolver>(new IProjectorResolver[]
            {
			    PrimitiveResolver,
			    new EnumResolver(),
			    TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementToStringDomainTranslator()),
			    new CollectionUnpackerTemplate<XElement>(new CollectionUnpacker(Root.AsRuntimeDomainNoncircular())),
			    new RecordUnpackerTemplate<ElementUnpackerState>(new FieldProjectorResolver(Root.AsRuntimeDomainNoncircular()), new AliasFieldProjectorResolver(Root.AsRuntimeDomainNoncircular()), new TagProjectorResolver(Root.AsRuntimeDomainNoncircular()), new TaggedUnionAlternativeProjectorResolver(Root.AsRuntimeDomainNoncircular()), new TupleElementProjectorResolver(Root.AsRuntimeDomainNoncircular())),
			    TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementProjectorToProjectorDomainTranslator())
		    });
            DebugResolver = new DebugReaderResolver(Mapping.CreateMapper(ProjectorResolverList.Concatenated(), Mapping.EmptyAggregatorResolver));
            Resolver = DebugResolver;
        }

        public void PutReader<T>(Func<String, T> Reader)
        {
            PrimitiveResolver.PutProjector(Reader);
        }
        public void PutReader<T>(Func<XElement, T> Reader)
        {
            PrimitiveResolver.PutProjector(Reader);
        }
        public void PutReaderTranslator<R, M>(IProjectorToProjectorRangeTranslator<R, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
        public void PutReaderTranslator<M>(IProjectorToProjectorDomainTranslator<XElement, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }

        public XElement CurrentReadingXElement
        {
            get { return DebugResolver.CurrentReadingXElement; }
        }
    }

    public class UISchemaXmlWriterResolver : IMapperResolver
    {
        private IMapperResolver Root;
        private PrimitiveResolver PrimitiveResolver;
        private IMapperResolver Resolver;
        private LinkedList<IProjectorResolver> ProjectorResolverList;

        private LinkedList<IAggregatorResolver> AggregatorResolverList;
        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveProjector(TypePair);
        }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveAggregator(TypePair);
        }

        public UISchemaXmlWriterResolver(IMapperResolver Root)
        {
            this.Root = Root;

            PrimitiveResolver = new PrimitiveResolver();

            PutWriter((byte b) => b.ToInvariantString());
            PutWriter((UInt16 i) => i.ToInvariantString());
            PutWriter((UInt32 i) => i.ToInvariantString());
            PutWriter((UInt64 i) => i.ToInvariantString());
            PutWriter((sbyte i) => i.ToInvariantString());
            PutWriter((Int16 i) => i.ToInvariantString());
            PutWriter((Int32 i) => i.ToInvariantString());
            PutWriter((Int64 i) => i.ToInvariantString());
            PutWriter((float f) => f.ToInvariantString());
            PutWriter((double f) => f.ToInvariantString());
            PutWriter((bool b) => b.ToInvariantString());
            PutWriter((String s) => s);
            PutWriter((decimal d) => d.ToInvariantString());

            //Writer
            //proj <- proj/aggr
            //PrimitiveResolver: (Primitive proj String|XElement) <- null
            //EnumResolver: (Enum proj String) <- null
            //XElementToStringRangeTranslator: (D proj XElement) <- (D proj String)
            //XElementAggregatorToProjectorRangeTranslator: (D proj XElement) <- (D aggr List(XElement))
            //
            //Writer
            //aggr <- proj/aggr
            //CollectionPacker: ({D} aggr Collection(XElement)) <- (D proj XElement)
            //FieldOrPropertyAggregatorResolver: (D aggr List(XElement)) <- (D.Field proj XElement)
            //XElementProjectorToAggregatorRangeTranslator: (D aggr List(XElement)) <- (D proj XElement)

            ProjectorResolverList = new LinkedList<IProjectorResolver>(new IProjectorResolver[]
            {
			    PrimitiveResolver,
			    new EnumResolver(),
			    TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementToStringRangeTranslator()),
			    TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementAggregatorToProjectorRangeTranslator())
		    });
            AggregatorResolverList = new LinkedList<IAggregatorResolver>(new IAggregatorResolver[]
            {
			    new CollectionPackerTemplate<ElementPackerState>(new CollectionPacker(Root.AsRuntimeDomainNoncircular())),
			    new RecordPackerTemplate<ElementPackerState>(new FieldAggregatorResolver(Root.AsRuntimeDomainNoncircular()), new AliasFieldAggregatorResolver(Root.AsRuntimeDomainNoncircular()), new TagAggregatorResolver(Root.AsRuntimeDomainNoncircular()), new TaggedUnionAlternativeAggregatorResolver(Root.AsRuntimeDomainNoncircular()), new TupleElementAggregatorResolver(Root.AsRuntimeDomainNoncircular())),
			    TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementProjectorToAggregatorRangeTranslator())
		    });
            Resolver = Mapping.CreateMapper(ProjectorResolverList.Concatenated(), AggregatorResolverList.Concatenated());
        }

        public void PutWriter<T>(Func<T, String> Writer)
        {
            PrimitiveResolver.PutProjector(Writer);
        }
        public void PutWriter<T>(Func<T, XElement> Writer)
        {
            PrimitiveResolver.PutProjector(Writer);
        }
        public void PutWriterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
        public void PutWriterTranslator<M>(IProjectorToProjectorRangeTranslator<XElement, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
    }

    public class XElementProjectorToProjectorDomainTranslator : IProjectorToProjectorDomainTranslator<XElement, ElementUnpackerState>
    {
        public Func<XElement, R> TranslateProjectorToProjectorDomain<R>(Func<ElementUnpackerState, R> Projector)
        {
            return Element =>
            {
                if (!Element.IsEmpty || (Element.Attributes().Count() > 0))
                {
                    var l = Element.Elements().ToList();
                    var d = new Dictionary<String, XElement>(StringComparer.OrdinalIgnoreCase);
                    foreach (var e in l)
                    {
                        var LocalName = e.Name.LocalName;
                        if (!d.ContainsKey(LocalName))
                        {
                            d.Add(LocalName, e);
                        }
                    }
                    var ad = new Dictionary<String, XAttribute>(StringComparer.OrdinalIgnoreCase);
                    foreach (var a in Element.Attributes())
                    {
                        var LocalName = a.Name.LocalName;
                        if (!ad.ContainsKey(LocalName))
                        {
                            ad.Add(LocalName, a);
                        }
                    }
                    return Projector(new ElementUnpackerState
                    {
                        Parent = Element,
                        List = l,
                        Dict = d,
                        AttributeDict = ad
                    });
                }
                else
                {
                    return default(R);
                }
            };
        }
    }

    public class XElementAggregatorToProjectorRangeTranslator : IAggregatorToProjectorRangeTranslator<XElement, ElementPackerState>
    {
        public Func<D, XElement> TranslateAggregatorToProjectorRange<D>(Action<D, ElementPackerState> Aggregator)
        {
            var FriendlyName = MappingXml.GetTypeFriendlyName(typeof(D));
            return v =>
            {
                XElement x = default(XElement);
                List<XNode> l = new List<XNode>();
                List<XAttribute> al = new List<XAttribute>();
                if (v != null)
                {
                    ElementPackerState s = new ElementPackerState
                    {
                        UseParent = false,
                        Parent = null,
                        List = l,
                        AttributeList = al
                    };
                    Aggregator(v, s);
                    if (typeof(D).GetCustomAttributes(typeof(Firefly.Mapping.MetaSchema.TaggedUnionAttribute), false).Length != 0)
                    {
                        if (l.Count == 1)
                        {
                            var a = l.Single() as XElement;
                            if (!FriendlyName.Equals(a.Name.LocalName))
                            {
                                return a;
                            }
                        }
                    }
                    if (s.UseParent)
                    {
                        x = s.Parent;
                        x.Name = FriendlyName;
                    }
                    else if (l.Count == 0)
                    {
                        x = new XElement(FriendlyName, "");
                    }
                    else
                    {
                        x = new XElement(FriendlyName, l.ToArray());
                    }
                }
                else
                {
                    x = new XElement(FriendlyName, null);
                }
                foreach (var a in al)
                {
                    x.SetAttributeValue(a.Name, a.Value);
                }
                return x;
            };
        }
    }

    public class FieldProjectorResolver : IFieldProjectorResolver<ElementUnpackerState>
    {
        private class OptionalCreator
        {
            public Delegate m;
            public Optional<RInner> Create<RInner>(String s)
            {
                return Optional<RInner>.CreateHasValue(((Func<String, RInner>)(m))(s));
            }
        }

        private Func<ElementUnpackerState, R> Resolve<R>(String Name)
        {
            var Mapper = (Func<XElement, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(XElement), typeof(R)));
            var AMapper = (Func<String, R>)InnerResolver.TryResolveProjector(CollectionOperations.CreatePair(typeof(String), typeof(R)));
            if (AMapper == null)
            {
                if (typeof(R).IsGenericType && (typeof(R).GetGenericTypeDefinition() == typeof(Optional<>)))
                {
                    var RInner = typeof(R).GetGenericArguments().Single();
                    var m = InnerResolver.TryResolveProjector(CollectionOperations.CreatePair(typeof(String), RInner));
                    if (m != null)
                    {
                        var c = new OptionalCreator { m = m };
                        var f = (Func<String, R>)(Delegate.CreateDelegate(typeof(Func<String, R>), c, ((Func<String, Optional<DummyType>>)(c.Create<DummyType>)).Method.MakeGenericMethodFromDummy(typeof(DummyType), RInner)));
                        AMapper = f;
                    }
                }
            }
            Func<ElementUnpackerState, R> F = (ElementUnpackerState s) =>
            {
                var ad = s.AttributeDict;
                if (ad.ContainsKey(Name))
                {
                    var v = ad[Name].Value;
                    if (AMapper != null)
                    {
                        return AMapper(v);
                    }
                }
                var d = s.Dict;
                if (d.ContainsKey(Name)) { return Mapper(d[Name]); }
                if (Name.Equals("Content"))
                {
                    if (typeof(R) == typeof(String))
                    {
                        var v = s.Parent.Value;
                        if (AMapper != null)
                        {
                            return AMapper(v);
                        }
                        else
                        {
                            return (R)(Object)(v);
                        }
                    }
                    else
                    {
                        return Mapper(s.Parent);
                    }
                }

                if (typeof(R).IsGenericType && (typeof(R).GetGenericTypeDefinition() == typeof(Optional<>)))
                {
                    return default(R);
                }

                FileLocationInformation i = new FileLocationInformation();
                var flip = s.Parent as IFileLocationInformationProvider;
                if (flip != null)
                {
                    i = flip.FileLocationInformation;
                }
                else
                {
                    var li = (IXmlLineInfo)s.Parent;
                    if (li.HasLineInfo())
                    {
                        i.LineNumber = li.LineNumber;
                        i.ColumnNumber = li.LinePosition;
                    }
                }
                throw new InvalidTextFormatException("FieldNameNotFound: {0}".Formats(Name), i);
            };
            return F;
        }

        private Dictionary<Type, Func<String, Delegate>> Dict = new Dictionary<Type, Func<String, Delegate>>();
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            var Name = Member.Name;
            if (Dict.ContainsKey(Type))
            {
                var m = Dict[Type];
                return m(Name);
            }
            else
            {
                var GenericMapper = (Func<String, Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<String, Delegate>();
                Dict.Add(Type, m);
                return m(Name);
            }
        }

        private IProjectorResolver InnerResolver;
        public FieldProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class FieldAggregatorResolver : IFieldAggregatorResolver<ElementPackerState>
    {
        private class OptionalTranslator
        {
            public Delegate m;
            public String Create<DInner>(Optional<DInner> o)
            {
                if (o.OnNotHasValue) { return null; }
                return ((Func<DInner, String>)(m))(o.HasValue);
            }
        }

        private Action<D, ElementPackerState> Resolve<D>(String Name)
        {
            var Mapper = (Func<D, XElement>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(XElement)));
            var AMapper = (Func<D, String>)InnerResolver.TryResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(String)));
            if (AMapper == null)
            {
                if (typeof(D).IsGenericType && (typeof(D).GetGenericTypeDefinition() == typeof(Optional<>)))
                {
                    var DInner = typeof(D).GetGenericArguments().Single();
                    var m = InnerResolver.TryResolveProjector(CollectionOperations.CreatePair(DInner, typeof(String)));
                    if (m != null)
                    {
                        var t = new OptionalTranslator { m = m };
                        var f = (Func<D, String>)(Delegate.CreateDelegate(typeof(Func<D, String>), t, ((Func<Optional<DummyType>, String>)(t.Create<DummyType>)).Method.MakeGenericMethodFromDummy(typeof(DummyType), DInner)));
                        AMapper = f;
                    }
                }
            }
            Action<D, ElementPackerState> F = (D k, ElementPackerState s) =>
            {
                if (AMapper != null)
                {
                    var at = AMapper(k);
                    if ((at == null) && typeof(D).IsGenericType && (typeof(D).GetGenericTypeDefinition() == typeof(Optional<>)))
                    {
                        return;
                    }
                    s.AttributeList.Add(new XAttribute(Name, at));
                    return;
                }
                if (Name.Equals("Content"))
                {
                    var e = Mapper(k);
                    if (typeof(D).GetCustomAttributes(typeof(Firefly.Mapping.MetaSchema.TaggedUnionAttribute), false).Length != 0)
                    {
                        s.List.Add(e);
                        return;
                    }
                    if (!e.HasAttributes)
                    {
                        if (e.HasElements)
                        {
                            s.List.AddRange(e.Elements());
                        }
                        else
                        {
                            s.Parent.Value = e.Value;
                        }
                        return;
                    }
                }
                {
                    var e = Mapper(k);
                    e.Name = Name;
                    s.List.Add(e);
                }
            };
            return F;
        }

        private Dictionary<Type, Func<String, Delegate>> Dict = new Dictionary<Type, Func<String, Delegate>>();
        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            var Name = Member.Name;
            if (Dict.ContainsKey(Type))
            {
                var m = Dict[Type];
                return m(Name);
            }
            else
            {
                var GenericMapper = (Func<String, Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<String, Delegate>();
                Dict.Add(Type, m);
                return m(Name);
            }
        }

        private IProjectorResolver InnerResolver;
        public FieldAggregatorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class AliasFieldProjectorResolver : IAliasFieldProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>()
        {
            var Mapper = (Func<XElement, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(XElement), typeof(R)));
            Func<ElementUnpackerState, R> F = (ElementUnpackerState s) => { return Mapper(s.Parent); };
            return F;
        }

        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            var GenericMapper = (Func<Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
            var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<Delegate>();
            return m();
        }

        private IProjectorResolver InnerResolver;
        public AliasFieldProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class AliasFieldAggregatorResolver : IAliasFieldAggregatorResolver<ElementPackerState>
    {
        private Action<D, ElementPackerState> Resolve<D>()
        {
            var Mapper = (Func<D, XElement>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(XElement)));
            Action<D, ElementPackerState> F = (D k, ElementPackerState s) =>
            {
                var e = Mapper(k);
                s.UseParent = true;
                s.Parent = e;
            };
            return F;
        }

        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            var GenericMapper = (Func<Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
            var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<Delegate>();
            return m();
        }

        private IProjectorResolver InnerResolver;
        public AliasFieldAggregatorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class TagProjectorResolver : ITagProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>()
        {
            var Mapper = (Func<String, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(String), typeof(R)));
            Func<ElementUnpackerState, R> F = (ElementUnpackerState s) =>
            {
                var EnumNames = typeof(R).GetEnumNames();
                if (EnumNames.Contains(s.Parent.Name.LocalName))
                {
                    var TagValue2 = s.Parent.Name.LocalName;
                    if (s.List.Count == 1)
                    {
                        var TagValue = s.List.Single().Name.LocalName;
                        if (EnumNames.Contains(TagValue))
                        {
                            FileLocationInformation i = new FileLocationInformation();
                            var flip = s.Parent as IFileLocationInformationProvider;
                            if (flip != null)
                            {
                                i = flip.FileLocationInformation;
                            }
                            else
                            {
                                var li = (IXmlLineInfo)s.Parent;
                                if (li.HasLineInfo())
                                {
                                    i.LineNumber = li.LineNumber;
                                    i.ColumnNumber = li.LinePosition;
                                }
                            }
                            throw new InvalidTextFormatException("TagNameConflict: {0}, {1}".Formats(TagValue2, TagValue), i);
                        }
                    }
                    return Mapper(TagValue2);
                }
                else
                {
                    var TagValue = s.List.Single().Name.LocalName;
                    return Mapper(TagValue);
                }
            };
            return F;
        }

        public Delegate ResolveProjector(MemberInfo Member, Type TagType)
        {
            var GenericMapper = (Func<Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
            var m = GenericMapper.MakeDelegateMethodFromDummy(TagType).AdaptFunction<Delegate>();
            return m();
        }

        private IProjectorResolver InnerResolver;
        public TagProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class TagAggregatorResolver : ITagAggregatorResolver<ElementPackerState>
    {
        private Action<D, ElementPackerState> Resolve<D>()
        {
            var Mapper = (Func<D, String>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(String)));
            Action<D, ElementPackerState> F = (D k, ElementPackerState s) => { };
            return F;
        }

        public Delegate ResolveAggregator(MemberInfo Member, Type TagType)
        {
            var GenericMapper = (Func<Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
            var m = GenericMapper.MakeDelegateMethodFromDummy(TagType).AdaptFunction<Delegate>();
            return m();
        }

        private IProjectorResolver InnerResolver;
        public TagAggregatorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class TaggedUnionAlternativeProjectorResolver : ITaggedUnionAlternativeProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>(String Name)
        {
            var Mapper = (Func<XElement, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(XElement), typeof(R)));
            Func<ElementUnpackerState, R> F = (ElementUnpackerState s) =>
            {
                var d = s.Dict;
                if (d.ContainsKey(Name)) { return Mapper(d[Name]); }

                if (s.Parent.Name.LocalName.Equals(Name)) { return Mapper(s.Parent); }

                FileLocationInformation i = new FileLocationInformation();
                var flip = s.Parent as IFileLocationInformationProvider;
                if (flip != null)
                {
                    i = flip.FileLocationInformation;
                }
                else
                {
                    var li = (IXmlLineInfo)s.Parent;
                    if (li.HasLineInfo())
                    {
                        i.LineNumber = li.LineNumber;
                        i.ColumnNumber = li.LinePosition;
                    }
                }
                throw new InvalidTextFormatException("AlternativeNameNotFound: {0}".Formats(Name), i);
            };
            return F;
        }

        private Dictionary<Type, Func<String, Delegate>> Dict = new Dictionary<Type, Func<String, Delegate>>();
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            var Name = Member.Name;
            if (Dict.ContainsKey(Type))
            {
                var m = Dict[Type];
                return m(Name);
            }
            else
            {
                var GenericMapper = (Func<String, Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<String, Delegate>();
                Dict.Add(Type, m);
                return m(Name);
            }
        }

        private IProjectorResolver InnerResolver;
        public TaggedUnionAlternativeProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class TaggedUnionAlternativeAggregatorResolver : ITaggedUnionAlternativeAggregatorResolver<ElementPackerState>
    {
        private Action<D, ElementPackerState> Resolve<D>(String Name)
        {
            var Mapper = (Func<D, XElement>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(XElement)));
            Action<D, ElementPackerState> F = (D k, ElementPackerState s) =>
            {
                var e = Mapper(k);
                e.Name = Name;
                s.List.Add(e);
            };
            return F;
        }

        private Dictionary<Type, Func<String, Delegate>> Dict = new Dictionary<Type, Func<String, Delegate>>();
        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            var Name = Member.Name;
            if (Dict.ContainsKey(Type))
            {
                var m = Dict[Type];
                return m(Name);
            }
            else
            {
                var GenericMapper = (Func<String, Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<String, Delegate>();
                Dict.Add(Type, m);
                return m(Name);
            }
        }

        private IProjectorResolver InnerResolver;
        public TaggedUnionAlternativeAggregatorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
}
