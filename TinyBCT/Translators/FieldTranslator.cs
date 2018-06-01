﻿using Microsoft.Cci;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test")]
namespace TinyBCT.Translators
{
    class FieldTranslator
    {
        IFieldReference fieldRef;
        internal static IDictionary<IFieldReference, String> fieldNames = new Dictionary<IFieldReference, String>();

        public static IList<String> GetFieldDefinitions()
        {
            IList<String> values = new List<String>();

            foreach (var item in fieldNames)
            {
                if (item.Key.IsStatic) {
                    values.Add(String.Format("var {0}: {1};", item.Value, Helpers.GetBoogieType(item.Key.Type)));
                } else {
                    if (!Settings.SplitFields)
                        values.Add(String.Format("const unique {0} : Field;", item.Value));
                    else
                    {
                        var boogieType = Helpers.GetBoogieType(item.Key.Type);
                        Contract.Assert(!string.IsNullOrEmpty(boogieType));
                        values.Add(String.Format("var {0} : [Ref]{1};", item.Value, boogieType));
                    }

                    // var F$ConsoleApplication3.Holds`1.x: [Ref]Ref;
                }
            }

            return values;
        }

        public static String GetFieldName(IFieldReference fieldRef)
        {
            if (fieldNames.ContainsKey(fieldRef))
                return fieldNames[fieldRef];

            FieldTranslator ft = new FieldTranslator(fieldRef);

            return ft.Translate();
        }

        public FieldTranslator(IFieldReference f)
        {
            fieldRef = f;
        }

        public String Translate()
        {
            var typeName = Helpers.GetNormalizedType(fieldRef.ContainingType);
            var fieldName = typeName + "." + fieldRef.Name.Value;
            var name = String.Format("F${0}", fieldName);
            name = Helpers.NormalizeStringForCorral(name);

            fieldNames.Add(fieldRef, name);
            return name;
        }
    }
}
