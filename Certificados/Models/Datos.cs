//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Certificados.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Datos
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Datos()
        {
            this.Plantilla_Dato = new HashSet<Plantilla_Dato>();
            this.Documentos = new HashSet<Documentos>();
        }
    
        public int Id { get; set; }
        public string fecha { get; set; }
        public string opcional1 { get; set; }
        public string opcional2 { get; set; }
        public string opcional3 { get; set; }
        public string opcional4 { get; set; }
        public System.DateTime fecha_registro { get; set; }
        public string nombres_apellidos { get; set; }
        public string curso_taller { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Plantilla_Dato> Plantilla_Dato { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Documentos> Documentos { get; set; }
    }
}
