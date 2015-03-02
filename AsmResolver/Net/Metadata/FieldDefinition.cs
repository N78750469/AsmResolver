﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsmResolver.Net.Builder;
using AsmResolver.Net.Signatures;

namespace AsmResolver.Net.Metadata
{
    public class FieldDefinitionTable : MetadataTable<FieldDefinition>
    {
        public override MetadataTokenType TokenType
        {
            get { return MetadataTokenType.Field; }
        }

        public override uint GetElementByteCount()
        {
            return sizeof (ushort) +                    // Attributes
                   (uint)TableStream.StringIndexSize +  // Name
                   (uint)TableStream.BlobIndexSize;     // Signature
        }

        protected override FieldDefinition ReadMember(MetadataToken token, ReadingContext context)
        {
            var reader = context.Reader;
            return new FieldDefinition(Header, token, new MetadataRow<ushort, uint, uint>
            {
                Column1 = reader.ReadUInt16(),                           // Attributes
                Column2 = reader.ReadIndex(TableStream.StringIndexSize), // Name
                Column3 = reader.ReadIndex(TableStream.BlobIndexSize),   // Signature
            });
        }

        protected override void UpdateMember(NetBuildingContext context, FieldDefinition member)
        {
            var row = member.MetadataRow;
            row.Column1 = (ushort)member.Attributes;
            row.Column2 = context.GetStreamBuffer<StringStreamBuffer>().GetStringOffset(member.Name);
            row.Column3 = context.GetStreamBuffer<BlobStreamBuffer>().GetBlobOffset(member.Signature);
        }

        protected override void WriteMember(WritingContext context, FieldDefinition member)
        {
            var writer = context.Writer;
            var row = member.MetadataRow;

            writer.WriteUInt16(row.Column1);
            writer.WriteIndex(TableStream.StringIndexSize, row.Column2);
            writer.WriteIndex(TableStream.BlobIndexSize, row.Column3);
        }
    }

    public class FieldDefinition : MetadataMember<MetadataRow<ushort, uint, uint>>, IHasConstant, IHasFieldMarshal, IMemberForwarded, ICollectionItem
    {
        private string _name;
        private string _fullName;
        private FieldSignature _signature;
        private CustomAttributeCollection _customAttributes;
        private TypeDefinition _declaringType;
        private Constant _constant;
        private FieldMarshal _marshal;
        private FieldRva _rva;

        public FieldDefinition(string name, FieldAttributes attributes, FieldSignature signature)
            : base(null, new MetadataToken(MetadataTokenType.Field), new MetadataRow<ushort, uint, uint>())
        {
            if (name == null)
                throw new ArgumentNullException("name");
            if (signature == null)
                throw new ArgumentNullException("signature");

            Name = name;
            Attributes = attributes;
            Signature = signature;
        }

        internal FieldDefinition(MetadataHeader header, MetadataToken token, MetadataRow<ushort, uint, uint> row)
            : base(header, token, row)
        {
            var stringStream = header.GetStream<StringStream>();

            Attributes = (FieldAttributes)row.Column1;
            Name = stringStream.GetStringByOffset(row.Column2);
            Signature = FieldSignature.FromReader(header, header.GetStream<BlobStream>().CreateBlobReader(row.Column3));
        }

        public FieldAttributes Attributes
        {
            get;
            set;
        }

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                _fullName = null;
            }
        }

        public virtual string FullName
        {
            get { return _fullName ?? (_fullName = this.GetFullName(Signature)); }
        }

        public TypeDefinition DeclaringType
        {
            get
            {
                if (_declaringType != null || Header == null)
                    return _declaringType;
                return _declaringType =
                    Header.GetStream<TableStream>()
                        .GetTable<TypeDefinition>()
                        .FirstOrDefault(x => x.Fields.Contains(this));
            }
        }

        ITypeDefOrRef IMemberReference.DeclaringType
        {
            get { return DeclaringType; }
        }

        public FieldSignature Signature
        {
            get { return _signature; }
            set
            {
                _signature = value;
                _fullName = null;
            }
        }

        public Constant Constant
        {
            get
            {
                if (_constant != null || Header == null)
                    return _constant;

                var table = Header.GetStream<TableStream>().GetTable<Constant>();
                return _constant = table.FirstOrDefault(x => x.Parent == this);
            }
            set { _constant = value; }
        }

        public FieldMarshal FieldMarshal
        {
            get
            {
                if (_marshal != null || Header == null)
                    return _marshal;

                var table = Header.GetStream<TableStream>().GetTable<FieldMarshal>();
                return _marshal = table.FirstOrDefault(x => x.Parent == this);
            }
            set { _marshal = value; }
        }

        public FieldRva FieldRva
        {
            get
            {
                if (_rva != null || Header == null)
                    return _rva;

                var table = Header.GetStream<TableStream>().GetTable<FieldRva>();
                return _rva = table.FirstOrDefault(x => x.Field == this);
            }
            set { _rva = value; }
        }
        
        public CustomAttributeCollection CustomAttributes
        {
            get
            {
                if (_customAttributes != null)
                    return _customAttributes;
                _customAttributes = new CustomAttributeCollection(this);
                return _customAttributes;
            }
        }

        object ICollectionItem.Owner
        {
            get { return DeclaringType; }
            set { _declaringType = value as TypeDefinition; }
        }

        public bool IsPrivate
        {
            get { return GetFieldAccessAttribute(FieldAttributes.Private); }
            set { SetFieldAccessAttribute(FieldAttributes.Private, value); }
        }

        public bool IsFamilyAndAssembly
        {
            get { return GetFieldAccessAttribute(FieldAttributes.FamilyAndAssembly); }
            set { SetFieldAccessAttribute(FieldAttributes.FamilyAndAssembly, value); }
        }

        public bool IsFamilyOrAssembly
        {
            get { return GetFieldAccessAttribute(FieldAttributes.FamilyOrAssembly); }
            set { SetFieldAccessAttribute(FieldAttributes.FamilyOrAssembly, value); }
        }

        public bool IsAssembly
        {
            get { return GetFieldAccessAttribute(FieldAttributes.Assembly); }
            set { SetFieldAccessAttribute(FieldAttributes.Assembly, value); }
        }

        public bool IsFamily
        {
            get { return GetFieldAccessAttribute(FieldAttributes.Family); }
            set { SetFieldAccessAttribute(FieldAttributes.Family, value); }
        }

        public bool IsPublic
        {
            get { return GetFieldAccessAttribute(FieldAttributes.Public); }
            set { SetFieldAccessAttribute(FieldAttributes.Public, value); }
        }

        public bool IsStatic
        {
            get { return ((uint)Attributes).GetAttribute((uint)FieldAttributes.Static); }
            set { Attributes = (FieldAttributes)((uint)Attributes).SetAttribute((uint)FieldAttributes.Static, value); }
        }

        private bool GetFieldAccessAttribute(FieldAttributes attribute)
        {
            return ((uint)Attributes).GetMaskedAttribute((uint)FieldAttributes.FieldAccessMask,
                (uint)attribute);
        }

        private void SetFieldAccessAttribute(FieldAttributes attribute, bool value)
        {
            Attributes = (FieldAttributes)((uint)Attributes).SetMaskedAttribute((uint)FieldAttributes.FieldAccessMask,
                (uint)attribute, value);
        }

        public override string ToString()
        {
            return FullName;
        }
    }
}
