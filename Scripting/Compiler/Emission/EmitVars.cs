using System;
using System.CodeDom;
using System.Reflection.Emit;
using IronAHK.Rusty;

namespace IronAHK.Scripting
{
    partial class MethodWriter
    {
        Type EmitVariableReference(CodeVariableReferenceExpression Expr)
        {
            if(!Locals.ContainsKey(Expr.VariableName))
                throw new CompileException(Expr, "Undefined variable: "+Expr.VariableName);

            LocalBuilder Builder = Locals[Expr.VariableName];
            Generator.Emit(OpCodes.Ldloc, Builder);

            return Builder.LocalType;
        }

        void EmitVariableDeclarationStatement(CodeVariableDeclarationStatement Statement)
        {
            if(Locals.ContainsKey(Statement.Name))
                throw new CompileException(Statement, "Attempt to redefine local variable "+Statement.Name);

            Type Top = EmitExpression(Statement.InitExpression);
            LocalBuilder Local = Generator.DeclareLocal(Top);
            Locals.Add(Statement.Name, Local);

            Generator.Emit(OpCodes.Stloc, Local);
        }

        void EmitArgumentReference(CodeArgumentReferenceExpression Argument)
        {
            Depth++;
            Debug("Emitting argument reference");
            Generator.Emit(OpCodes.Ldarg, 0); // for now only used to refer to the sole object[] parameter
            Depth--;
        }

        void EmitAssignStatement(CodeAssignStatement Assign, bool ForceTypes)
        {
            EmitAssignment(Assign.Left, Assign.Right, ForceTypes);
        }

        void EmitAssignment(CodeExpression Left, CodeExpression Right, bool ForceTypes)
        {
            Depth++;
            Debug("Emitting assignment statement");

            if(Left is CodeVariableReferenceExpression)
            {
                // local IL variables generated by parser
                var Reference = Left as CodeVariableReferenceExpression;

                LocalBuilder Var;
                if(Locals.ContainsKey(Reference.VariableName))
                    Var = Locals[Reference.VariableName];
                else
                {
                    Var = Generator.DeclareLocal(typeof(int));
                    Locals.Add(Reference.VariableName, Var);
                }

                EmitExpression(Right, ForceTypes);
                Generator.Emit(OpCodes.Stloc, Var);
                Generator.Emit(OpCodes.Pop);
            }
            else if (Left is CodeArrayIndexerExpression)
            {
                var index = (CodeArrayIndexerExpression)Left;

                // HACK: generic way of setting indexers from properties (cil)

                var vars = typeof(Script).GetProperty(Parser.VarProperty);
                Generator.Emit(OpCodes.Call, vars.GetGetMethod());

                EmitExpression(index.Indices[0]);
                EmitExpression(Right, ForceTypes);
                
                // "Item" is the property for this-indexers
                Generator.Emit(OpCodes.Callvirt, vars.PropertyType.GetProperty("Item").GetSetMethod());
            }
            else throw new CompileException(Left, "Left hand is unassignable");

            Depth--;
        }
        
        Type EmitDynamicName(CodeArrayCreateExpression Dynamic)
        {
            Depth++;
            Debug("Emitting dynamic name expression");
            
            Type ElementType = Type.GetType(Dynamic.CreateType.BaseType);
            EmitArrayCreation(ElementType, Dynamic.Initializers.Count);
            
            for(int i = 0; i < Dynamic.Initializers.Count; i++)
                EmitArrayInitializer(ElementType, Dynamic.Initializers[i], i);
                                     
            Depth--;
            
            return ElementType.MakeArrayType();
        }   
        
        Type EmitArrayCreation(Type ArrayType, int Count)
        {
            Generator.Emit(OpCodes.Ldc_I4, Count);
            Generator.Emit(OpCodes.Newarr, ArrayType);
            return ArrayType.MakeArrayType();
        }
            
        Type EmitArrayInitializer(Type ElementType, CodeExpression Expr, int Index)
        {
            Generator.Emit(OpCodes.Dup);
            Generator.Emit(OpCodes.Ldc_I4, Index);
            Type Generated = EmitExpression(Expr);
            ForceTopStack(Generated, ElementType);
            Generator.Emit(OpCodes.Stelem_Ref);
            return ElementType.MakeArrayType();
        }

        void ConditionalBox(Type Top)
        {
            if(Top == null)
                throw new Exception("Top type can not be null");
            
            if(Top != typeof(object)) Generator.Emit(OpCodes.Box, Top);
        }

        void ForceTopStack(Type Top, Type Wanted)
        {
            ForceTopStack(Top, Wanted, true);
        }

        void ForceTopStack(Type Top, Type Wanted, bool ForceTypes)
        {
            Depth++;
            if(Top != Wanted)
            {
                Debug("Forcing top stack "+Top+" to "+Wanted);
                if(Wanted == typeof(string))
                {
                    ConditionalBox(Top);
                    Generator.Emit(OpCodes.Call, ForceString);
                }
                else if (Wanted == typeof(decimal))
                {
                    ConditionalBox(Top);
                    Generator.Emit(OpCodes.Call, ForceDecimal);
                }
                else if (Wanted == typeof(long))
                {
                    ConditionalBox(Top);
                    Generator.Emit(OpCodes.Call, ForceLong);
                }
                else if (Wanted == typeof(int))
                {
                    ConditionalBox(Top);
                    Generator.Emit(OpCodes.Call, ForceInt);
                }
                else if (Wanted == typeof(bool))
                {
                    ConditionalBox(Top);
                    Generator.Emit(OpCodes.Call, ForceBool);
                }
                else if (Wanted == typeof(object))
                {
                    ConditionalBox(Top);
                }
                else if (Wanted == typeof(object[]) && Top.IsArray)
                {
                }
                else
                {
                    Debug("WARNING: Can not force " + Wanted);
                }
            }
            Depth--;
        }
    }
}
