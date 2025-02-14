﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Symbols;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Emit
{
    internal sealed class PEDeltaAssemblyBuilder : PEAssemblyBuilderBase, IPEDeltaAssemblyBuilder
    {
        private readonly EmitBaseline _previousGeneration;
        private readonly CSharpDefinitionMap _previousDefinitions;
        private readonly SymbolChanges _changes;
        private readonly CSharpSymbolMatcher.DeepTranslator _deepTranslator;

        public PEDeltaAssemblyBuilder(
            SourceAssemblySymbol sourceAssembly,
            EmitOptions emitOptions,
            OutputKind outputKind,
            Cci.ModulePropertiesForSerialization serializationProperties,
            IEnumerable<ResourceDescription> manifestResources,
            EmitBaseline previousGeneration,
            IEnumerable<SemanticEdit> edits,
            Func<ISymbol, bool> isAddedSymbol)
            : base(sourceAssembly, emitOptions, outputKind, serializationProperties, manifestResources, additionalTypes: ImmutableArray<NamedTypeSymbol>.Empty)
        {
            var initialBaseline = previousGeneration.InitialBaseline;
            var context = new EmitContext(this, null, new DiagnosticBag(), metadataOnly: false, includePrivateMembers: true);

            // Hydrate symbols from initial metadata. Once we do so it is important to reuse these symbols across all generations,
            // in order for the symbol matcher to be able to use reference equality once it maps symbols to initial metadata.
            var metadataSymbols = GetOrCreateMetadataSymbols(initialBaseline, sourceAssembly.DeclaringCompilation);
            var metadataDecoder = (MetadataDecoder)metadataSymbols.MetadataDecoder;
            var metadataAssembly = (PEAssemblySymbol)metadataDecoder.ModuleSymbol.ContainingAssembly;

            var matchToMetadata = new CSharpSymbolMatcher(
                metadataSymbols.AnonymousTypes,
                metadataSymbols.AnonymousDelegates,
                metadataSymbols.AnonymousDelegatesWithIndexedNames,
                sourceAssembly,
                context,
                metadataAssembly);

            CSharpSymbolMatcher? matchToPrevious = null;
            if (previousGeneration.Ordinal > 0)
            {
                RoslynDebug.AssertNotNull(previousGeneration.Compilation);
                RoslynDebug.AssertNotNull(previousGeneration.PEModuleBuilder);

                var previousAssembly = ((CSharpCompilation)previousGeneration.Compilation).SourceAssembly;
                var previousContext = new EmitContext((PEModuleBuilder)previousGeneration.PEModuleBuilder, null, new DiagnosticBag(), metadataOnly: false, includePrivateMembers: true);

                matchToPrevious = new CSharpSymbolMatcher(
                    previousGeneration.AnonymousTypeMap,
                    previousGeneration.AnonymousDelegates,
                    previousGeneration.AnonymousDelegatesWithIndexedNames,
                    sourceAssembly: sourceAssembly,
                    sourceContext: context,
                    otherAssembly: previousAssembly,
                    otherContext: previousContext,
                    otherSynthesizedMembers: previousGeneration.SynthesizedMembers,
                    otherDeletedMembers: previousGeneration.DeletedMembers);
            }

            _previousDefinitions = new CSharpDefinitionMap(edits, metadataDecoder, matchToMetadata, matchToPrevious);
            _previousGeneration = previousGeneration;
            _changes = new CSharpSymbolChanges(_previousDefinitions, edits, isAddedSymbol);

            // Workaround for https://github.com/dotnet/roslyn/issues/3192.
            // When compiling state machine we stash types of awaiters and state-machine hoisted variables,
            // so that next generation can look variables up and reuse their slots if possible.
            //
            // When we are about to allocate a slot for a lifted variable while compiling the next generation
            // we map its type to the previous generation and then check the slot types that we stashed earlier.
            // If the variable type matches we reuse it. In order to compare the previous variable type with the current one
            // both need to be completely lowered (translated). Standard translation only goes one level deep. 
            // Generic arguments are not translated until they are needed by metadata writer. 
            //
            // In order to get the fully lowered form we run the type symbols of stashed variables through a deep translator
            // that translates the symbol recursively.
            _deepTranslator = new CSharpSymbolMatcher.DeepTranslator(sourceAssembly.GetSpecialType(SpecialType.System_Object));
        }

        public override SymbolChanges? EncSymbolChanges => _changes;
        public override EmitBaseline PreviousGeneration => _previousGeneration;

        internal override Cci.ITypeReference EncTranslateLocalVariableType(TypeSymbol type, DiagnosticBag diagnostics)
        {
            // Note: The translator is not aware of synthesized types. If type is a synthesized type it won't get mapped.
            // In such case use the type itself. This can only happen for variables storing lambda display classes.
            var visited = (TypeSymbol)_deepTranslator.Visit(type);
            Debug.Assert((object)visited != null);
            //Debug.Assert(visited != null || type is LambdaFrame || ((NamedTypeSymbol)type).ConstructedFrom is LambdaFrame);
            return Translate(visited ?? type, null, diagnostics);
        }

        private static EmitBaseline.MetadataSymbols GetOrCreateMetadataSymbols(EmitBaseline initialBaseline, CSharpCompilation compilation)
        {
            if (initialBaseline.LazyMetadataSymbols != null)
            {
                return initialBaseline.LazyMetadataSymbols;
            }

            var originalMetadata = initialBaseline.OriginalMetadata;

            // The purpose of this compilation is to provide PE symbols for original metadata.
            // We need to transfer the references from the current source compilation but don't need its syntax trees.
            var metadataCompilation = compilation.RemoveAllSyntaxTrees();

            ImmutableDictionary<AssemblyIdentity, AssemblyIdentity> assemblyReferenceIdentityMap;
            var metadataAssembly = metadataCompilation.GetBoundReferenceManager().CreatePEAssemblyForAssemblyMetadata(AssemblyMetadata.Create(originalMetadata), MetadataImportOptions.All, out assemblyReferenceIdentityMap);
            var metadataDecoder = new MetadataDecoder(metadataAssembly.PrimaryModule);
            GetAnonymousTypeMapFromMetadata(originalMetadata.MetadataReader, metadataDecoder, out var metadataAnonymousTypes, out var metadataAnonymousDelegatesWithIndexedNames);
            var metadataAnonymousDelegates = GetAnonymousDelegateMapFromMetadata(originalMetadata.MetadataReader, metadataDecoder);
            var metadataSymbols = new EmitBaseline.MetadataSymbols(metadataAnonymousTypes, metadataAnonymousDelegates, metadataAnonymousDelegatesWithIndexedNames, metadataDecoder, assemblyReferenceIdentityMap);

            return InterlockedOperations.Initialize(ref initialBaseline.LazyMetadataSymbols, metadataSymbols);
        }

        // internal for testing
        internal static void GetAnonymousTypeMapFromMetadata(
            MetadataReader reader,
            MetadataDecoder metadataDecoder,
            out IReadOnlyDictionary<AnonymousTypeKey, AnonymousTypeValue> anonymousTypes,
            out IReadOnlyDictionary<string, AnonymousTypeValue> anonymousDelegatesWithIndexedNames)
        {
            // In general, the anonymous type name is "<{module-id}>f__AnonymousType{index}#{submission-index}",
            // but EnC is not supported for modules nor submissions. Hence we only look for type names with no module id and no submission index.
            const string AnonymousTypeOrDelegateNamePrefix = "<>f__Anonymous";
            const string AnonymousTypeNameWithoutModulePrefix = AnonymousTypeOrDelegateNamePrefix + "Type";
            const string AnonymousDelegateNameWithoutModulePrefix = AnonymousTypeOrDelegateNamePrefix + "Delegate";

            var types = new Dictionary<AnonymousTypeKey, AnonymousTypeValue>();
            var delegates = new Dictionary<string, AnonymousTypeValue>();

            foreach (var handle in reader.TypeDefinitions)
            {
                var def = reader.GetTypeDefinition(handle);
                if (!def.Namespace.IsNil)
                {
                    continue;
                }

                if (!reader.StringComparer.StartsWith(def.Name, AnonymousTypeOrDelegateNamePrefix))
                {
                    continue;
                }

                var metadataName = reader.GetString(def.Name);
                var name = MetadataHelpers.InferTypeArityAndUnmangleMetadataName(metadataName, out _);

                if (name.StartsWith(AnonymousTypeNameWithoutModulePrefix, StringComparison.Ordinal))
                {
                    if (int.TryParse(name.Substring(AnonymousTypeNameWithoutModulePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                    {
                        var builder = ArrayBuilder<AnonymousTypeKeyField>.GetInstance();
                        if (TryGetAnonymousTypeKey(reader, def, builder))
                        {
                            var type = (NamedTypeSymbol)metadataDecoder.GetTypeOfToken(handle);
                            var key = new AnonymousTypeKey(builder.ToImmutable());
                            var value = new AnonymousTypeValue(name, index, type.GetCciAdapter());
                            types.Add(key, value);
                        }
                        builder.Free();
                    }
                }
                else if (name.StartsWith(AnonymousDelegateNameWithoutModulePrefix, StringComparison.Ordinal))
                {
                    if (int.TryParse(name.Substring(AnonymousDelegateNameWithoutModulePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                    {
                        var type = (NamedTypeSymbol)metadataDecoder.GetTypeOfToken(handle);
                        var value = new AnonymousTypeValue(name, index, type.GetCciAdapter());
                        delegates.Add(name, value);
                    }
                }
            }

            anonymousTypes = types;
            anonymousDelegatesWithIndexedNames = delegates;
        }

        // internal for testing
        internal static IReadOnlyDictionary<SynthesizedDelegateKey, SynthesizedDelegateValue> GetAnonymousDelegateMapFromMetadata(MetadataReader reader, MetadataDecoder metadataDecoder)
        {
            var result = new Dictionary<SynthesizedDelegateKey, SynthesizedDelegateValue>();
            foreach (var handle in reader.TypeDefinitions)
            {
                var def = reader.GetTypeDefinition(handle);
                if (!def.Namespace.IsNil)
                {
                    continue;
                }

                if (!reader.StringComparer.StartsWith(def.Name, GeneratedNames.ActionDelegateNamePrefix) &&
                    !reader.StringComparer.StartsWith(def.Name, GeneratedNames.FuncDelegateNamePrefix))
                {
                    continue;
                }

                // The name of a synthesized delegate neatly encodes everything we need to identify it, either
                // in the prefix (return void or not) or the name (ref kinds and arity) so we don't need anything
                // fancy for a key.
                var metadataName = reader.GetString(def.Name);
                var key = new SynthesizedDelegateKey(metadataName);

                var type = (NamedTypeSymbol)metadataDecoder.GetTypeOfToken(handle);
                var value = new SynthesizedDelegateValue(type.GetCciAdapter());
                result.Add(key, value);
            }
            return result;
        }

        private static bool TryGetAnonymousTypeKey(
            MetadataReader reader,
            TypeDefinition def,
            ArrayBuilder<AnonymousTypeKeyField> builder)
        {
            foreach (var typeParameterHandle in def.GetGenericParameters())
            {
                var typeParameter = reader.GetGenericParameter(typeParameterHandle);
                if (!GeneratedNameParser.TryParseAnonymousTypeParameterName(reader.GetString(typeParameter.Name), out var fieldName))
                {
                    return false;
                }

                builder.Add(new AnonymousTypeKeyField(fieldName, isKey: false, ignoreCase: false));
            }
            return true;
        }

        internal CSharpDefinitionMap PreviousDefinitions
        {
            get { return _previousDefinitions; }
        }

        internal override bool SupportsPrivateImplClass
        {
            get
            {
                // Disable <PrivateImplementationDetails> in ENC since the
                // CLR does not support adding non-private members.
                return false;
            }
        }

        public IReadOnlyDictionary<AnonymousTypeKey, AnonymousTypeValue> GetAnonymousTypeMap()
        {
            var anonymousTypes = this.Compilation.AnonymousTypeManager.GetAnonymousTypeMap();
            // Should contain all entries in previous generation.
            Debug.Assert(_previousGeneration.AnonymousTypeMap.All(p => anonymousTypes.ContainsKey(p.Key)));
            return anonymousTypes;
        }

        public IReadOnlyDictionary<SynthesizedDelegateKey, SynthesizedDelegateValue> GetAnonymousDelegates()
        {
            var anonymousDelegates = this.Compilation.AnonymousTypeManager.GetAnonymousDelegates();
            // Should contain all entries in previous generation.
            Debug.Assert(_previousGeneration.AnonymousDelegates.All(p => anonymousDelegates.ContainsKey(p.Key)));
            return anonymousDelegates;
        }

        public IReadOnlyDictionary<string, AnonymousTypeValue> GetAnonymousDelegatesWithIndexedNames()
        {
            var anonymousDelegates = this.Compilation.AnonymousTypeManager.GetAnonymousDelegatesWithIndexedNames();
            // Should contain all entries in previous generation.
            Debug.Assert(_previousGeneration.AnonymousDelegatesWithIndexedNames.All(p => anonymousDelegates.ContainsKey(p.Key)));
            return anonymousDelegates;
        }

        public override IEnumerable<Cci.INamespaceTypeDefinition> GetTopLevelTypeDefinitions(EmitContext context)
        {
            foreach (var typeDef in GetAnonymousTypeDefinitions(context))
            {
                yield return typeDef;
            }

            foreach (var typeDef in GetTopLevelTypeDefinitionsCore(context))
            {
                yield return typeDef;
            }
        }

        public override IEnumerable<Cci.INamespaceTypeDefinition> GetTopLevelSourceTypeDefinitions(EmitContext context)
        {
            return _changes.GetTopLevelSourceTypeDefinitions(context);
        }

        internal override VariableSlotAllocator? TryCreateVariableSlotAllocator(MethodSymbol method, MethodSymbol topLevelMethod, DiagnosticBag diagnostics)
        {
            return _previousDefinitions.TryCreateVariableSlotAllocator(_previousGeneration, Compilation, method, topLevelMethod, diagnostics);
        }

        internal override ImmutableArray<AnonymousTypeKey> GetPreviousAnonymousTypes()
        {
            return ImmutableArray.CreateRange(_previousGeneration.AnonymousTypeMap.Keys);
        }

        internal override ImmutableArray<SynthesizedDelegateKey> GetPreviousAnonymousDelegates()
        {
            return ImmutableArray.CreateRange(_previousGeneration.AnonymousDelegates.Keys);
        }

        internal override int GetNextAnonymousTypeIndex()
        {
            return _previousGeneration.GetNextAnonymousTypeIndex();
        }

        internal override bool TryGetAnonymousTypeName(AnonymousTypeManager.AnonymousTypeTemplateSymbol template, [NotNullWhen(true)] out string? name, out int index)
        {
            Debug.Assert(this.Compilation == template.DeclaringCompilation);
            return _previousDefinitions.TryGetAnonymousTypeName(template, out name, out index);
        }

        public void OnCreatedIndices(DiagnosticBag diagnostics)
        {
            var embeddedTypesManager = this.EmbeddedTypesManagerOpt;
            if (embeddedTypesManager != null)
            {
                foreach (var embeddedType in embeddedTypesManager.EmbeddedTypesMap.Keys)
                {
                    diagnostics.Add(new CSDiagnosticInfo(ErrorCode.ERR_EncNoPIAReference, embeddedType.AdaptedSymbol), Location.None);
                }
            }
        }
    }
}
