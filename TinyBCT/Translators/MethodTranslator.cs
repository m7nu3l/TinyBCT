﻿using Backend;
using Backend.Analyses;
using Backend.Model;
using Backend.Transformations;
using Backend.Utils;
using Microsoft.Cci;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyBCT.Translators;

namespace TinyBCT
{
    class MethodTranslator
    {
        IMethodDefinition methodDefinition;
        MethodBody methodBody;
        ClassHierarchyAnalysis CHA;

        public static void transformBody(MethodBody methodBody)
        {
            var cfAnalysis = new ControlFlowAnalysis(methodBody);
            //var cfg = cfAnalysis.GenerateNormalControlFlow();
            Traverser.CFG = cfAnalysis.GenerateExceptionalControlFlow();

            var splitter = new WebAnalysis(Traverser.CFG, methodBody.MethodDefinition);
            splitter.Analyze();
            splitter.Transform();

            methodBody.UpdateVariables();

            var typeAnalysis = new TypeInferenceAnalysis(Traverser.CFG, methodBody.MethodDefinition.Type);
            typeAnalysis.Analyze();

            /*var forwardCopyAnalysis = new ForwardCopyPropagationAnalysis(CFG);
            forwardCopyAnalysis.Analyze();
            forwardCopyAnalysis.Transform(methodBody);

            var backwardCopyAnalysis = new BackwardCopyPropagationAnalysis(CFG);
            backwardCopyAnalysis.Analyze();
            backwardCopyAnalysis.Transform(methodBody);*/

            // TinyBCT transformations

            var refAlias = new RefAlias(methodBody);
            refAlias.Transform();

            var immutableArguments = new ImmutableArguments(methodBody);
            immutableArguments.Transform();

            var fieldInitialization = new FieldInitialization(methodBody);
            fieldInitialization.Transform();

            methodBody.RemoveUnusedLabels();
        }


        static bool whitelistContains(string name)
        {
            if (!Settings.DebugLargeDLL)
            {
                return true;
            }
            if (name.Contains("Analyzer1"))
                return true;
            if (name.Contains("Project"))
                return true;
            if (name.Contains("Diagnostic"))
                return true;

            if (name.Contains("MetadataReference"))
                return true;
            if (name.Contains("Location"))
                return true;
            if (name.Contains("Compilation"))
                return true;
            if (name.Contains("Document"))
                return true;

            return false;
        }
        // called from Traverser
        // set in Main
        public static void IMethodDefinitionTraverse(IMethodDefinition mD, IMetadataHost host, ISourceLocationProvider sourceLocationProvider)
        {
            if (!mD.IsExternal)
            {
                try
                {
                    if (whitelistContains(mD.ContainingType.FullName()))
                    {
                        var disassembler = new Disassembler(host, mD, sourceLocationProvider);
                        MethodBody mB = disassembler.Execute();
                        transformBody(mB);

                        MethodTranslator methodTranslator = new MethodTranslator(mD, mB, Traverser.CHA);
                        // todo: improve this piece of code
                        StreamWriter streamWriter = Program.streamWriter;
                        streamWriter.WriteLine(methodTranslator.Translate());
                        Helpers.addTranslatedMethod(mD);
                    }
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("WARNING: Exception thrown while translating method (omitting): " + Helpers.GetMethodName(mD));
                }
            }

        }

        public MethodTranslator(IMethodDefinition methodDefinition, MethodBody methodBody, ClassHierarchyAnalysis CHA)
        {
            this.methodDefinition = methodDefinition;
            this.methodBody = methodBody;
            this.CHA = CHA;
        }

        String TranslateInstructions()
        {
            InstructionTranslator instTranslator = new InstructionTranslator(this.CHA, methodBody);
            instTranslator.Translate();

            foreach (var v in instTranslator.RemovedVariables)
                methodBody.Variables.Remove(v);

            foreach (var v in instTranslator.AddedVariables)
                methodBody.Variables.Add(v);

            return instTranslator.Boogie();
        }

        String TranslateLocalVariables()
        {
            StringBuilder localVariablesSb = new StringBuilder();
            // local variables declaration - arguments are already declared
            methodBody.Variables.Except(methodBody.Parameters)//.Where(v => v.Type!=null)
                .Select(v =>
                        String.Format("\tvar {0} : {1};", v.Name, Helpers.GetBoogieType(v.Type))
                ).ToList().ForEach(str => localVariablesSb.AppendLine(str));

            // replace for class generated by compiler
            return localVariablesSb.ToString().Replace("<>", "__");
        }

        String TranslateAttr()
        {
            // commented entrypoint tag - this is only added to the wrappers
            // check StaticInitializer
            return /*Helpers.IsMain(methodDefinition) ? " {:entrypoint}" :*/ String.Empty;

        }

        String TranslateReturnTypeIfAny()
        {
            if (Helpers.GetMethodBoogieReturnType(methodDefinition).Equals("Void") && !methodDefinition.Parameters.Any(p => p.IsByReference || p.IsOut))
            {
                return String.Empty;
            }
            else
            {
                var returnVariables = new List<String>();
                returnVariables = methodDefinition.Parameters.Where(p => p.IsByReference).Select(p => String.Format("{0}$out : {1}", p.Name.Value, Helpers.GetBoogieType(p.Type))).ToList();
                if (!Helpers.GetMethodBoogieReturnType(methodDefinition).Equals("Void"))
                    returnVariables.Add(String.Format("$result : {0}", Helpers.GetMethodBoogieReturnType(methodDefinition)));

                return String.Format("returns ({0})", String.Join(",", returnVariables));
            }
        }

        public String Translate()
        {
            // instructions must be translated before local variables
            // modification to local variables can ocurr while instruction translation is done
            // for example when delegate creation is detected some local variables are deleted.

            var ins = TranslateInstructions();
            var localVariables = TranslateLocalVariables();
            var methodName = Helpers.GetMethodName(methodDefinition);
            var attr = TranslateAttr();
            var parametersWithTypes = Helpers.GetParametersWithBoogieType(methodDefinition);
            var returnTypeIfAny = TranslateReturnTypeIfAny();

            var boogieProcedureTemplate = new BoogieProcedureTemplate(methodName, attr, localVariables, ins, parametersWithTypes, returnTypeIfAny, Helpers.IsExternal(methodDefinition));
            return boogieProcedureTemplate.TransformText();
        }

    }
}
