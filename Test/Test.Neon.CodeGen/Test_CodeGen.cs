﻿//-----------------------------------------------------------------------------
// FILE:	    Test_CodeGen
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen.CodeGen
{
    public interface EmptyData
    {
    }

    public enum MyEnum1
    {
        One,
        Two,
        Three
    }

    [Flags]
    public enum MyEnum2 : int
    {
        [EnumMember(Value = "one")]
        One = 1,
        [EnumMember(Value = "two")]
        Two = 2,
        [EnumMember(Value = "three")]
        Three = 3
    }

    public interface SimpleData
    {
        string Name { get; set; }
        int Age { get; set; }
        MyEnum1 Enum { get; set; }
    }

    public interface BasicTypes
    {
        bool Bool { get; set; }
        byte Byte { get; set; }
        sbyte SByte { get; set; }
        short Short { get; set; }
        ushort UShort { get; set; }
        int Int { get; set; }
        uint UInt { get; set; }
        long Long { get; set; }
        ulong ULong { get; set; }
        float Float { get; set; }
        double Double { get; set; }
        decimal Decimal { get; set; }
        string String { get; set; }
    }

    public interface ComplexData
    {
        List<string> Items { get; set; }
        Dictionary<string, int> Lookup { get; set; }
        MyEnum1 Enum1 { get; set; }
        MyEnum2 Enum2 { get; set; }
        SimpleData Simple { get; set; }
        int[] SingleArray { get; set; }
        int[][] DoubleArray { get; set; }
        int[][][] TripleArray { get; set; }

        [JsonIgnore]
        int IgnoreThis { get; set; }
    }

    public interface NoSetter
    {
        string Value { get; }
    }

    public interface ParentModel
    {
        string ParentProperty { get; set; }
    }

    public interface ChildModel : ParentModel
    {
        string ChildProperty { get; set; }
    }

    public interface DefaultValues
    {
        [DefaultValue("Joe Bloe")]
        string Name { get; set; }

        [DefaultValue(67)]
        int Age { get; set; }

        [DefaultValue(true)]
        bool IsRetired { get; set; }

        [DefaultValue(100000)]
        double NetWorth { get; set; }

        [DefaultValue(MyEnum1.Three)]
        MyEnum1 Enum1 { get; set; }
    }

    [NoCodeGen]
    public class Test_DataModel
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void DataModel_Empty()
        {
            // Verify that we can generate code for an empty data model.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients   = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<EmptyData>();
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<EmptyData>("{}");
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));

                data = context.CreateDataWrapperFrom<EmptyData>(new JObject());
                Assert.Equal("{}", data.ToString());
                Assert.Equal("{}", data.ToString(indented: true));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void DataModel_Simple()
        {
            // Verify that we can generate code for a simple data model.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<SimpleData>();
                Assert.Equal("{\"Name\":null,\"Age\":0,\"Enum\":\"One\"}", data.ToString());
                Assert.Equal("{\r\n  \"Name\": null,\r\n  \"Age\": 0,\r\n  \"Enum\": \"One\"\r\n}", data.ToString(indented: true));

                data = context.CreateDataWrapper<SimpleData>();
                data["Name"] = "Jeff";
                data["Age"]  = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}", data.ToString());

                data = context.CreateDataWrapperFrom<SimpleData>("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}");
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}", data.ToString());

                var jObject = data.ToJObject();
                data = context.CreateDataWrapperFrom<SimpleData>(jObject);
                data["Name"] = "Jeff";
                data["Age"] = 58;
                data["Enum"] = MyEnum1.Two;
                Assert.Equal("{\"Name\":\"Jeff\",\"Age\":58,\"Enum\":\"Two\"}", data.ToString());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void DataModel_BasicTypes()
        {
            // Verify that we can generate code for basic data types.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<BasicTypes>();
                Assert.Equal("{\"Bool\":false,\"Byte\":0,\"SByte\":0,\"Short\":0,\"UShort\":0,\"Int\":0,\"UInt\":0,\"Long\":0,\"ULong\":0,\"Float\":0.0,\"Double\":0.0,\"Decimal\":0.0,\"String\":null}", data.ToString());

                data["Bool"]    = true;
                data["Byte"]    = (byte)1;
                data["SByte"]   = (sbyte)2;
                data["Short"]   = (short)3;
                data["UShort"]  = (ushort)4;
                data["Int"]     = (int)5;
                data["UInt"]    = (uint)6;
                data["Long"]    = (long)7;
                data["ULong"]   = (ulong)8;
                data["Float"]   = (float)9;
                data["Double"]  = (double)10;
                data["Decimal"] = (decimal)11;
                data["String"]  = "12";

                Assert.Equal("{\"Bool\":true,\"Byte\":1,\"SByte\":2,\"Short\":3,\"UShort\":4,\"Int\":5,\"UInt\":6,\"Long\":7,\"ULong\":8,\"Float\":9.0,\"Double\":10.0,\"Decimal\":11.0,\"String\":\"12\"}", data.ToString());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void DataModel_Complex()
        {
            // Verify that we can generate code for complex data types.

            var settings = new CodeGeneratorSettings()
            {
                SourceNamespace = typeof(Test_DataModel).Namespace,
                ServiceClients  = false
            };

            var generator = new CodeGenerator(settings);
            var output    = generator.Generate(Assembly.GetExecutingAssembly());

            Assert.False(output.HasErrors);

            var assemblyStream = CodeGenerator.Compile(output.SourceCode, "test-assembly", references => CodeGenTestHelper.ReferenceHandler(references));

            using (var context = new AssemblyContext("Neon.CodeGen.Output", assemblyStream))
            {
                var data = context.CreateDataWrapper<ComplexData>();

                var s = data.ToString();

                Assert.Equal("{\"Bool\":false,\"Byte\":0,\"SByte\":0,\"Short\":0,\"UShort\":0,\"Int\":0,\"UInt\":0,\"Long\":0,\"ULong\":0,\"Float\":0.0,\"Double\":0.0,\"Decimal\":0.0,\"String\":null}", data.ToString());
            }
        }
    }
}