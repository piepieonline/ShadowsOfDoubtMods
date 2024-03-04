using System;
using System.Collections.Generic;

namespace ApartmentRent
{

    [Serializable]
    class RentSaveFile
    {
        public int version { get; } = 1;
        public List<ApartmentSaveFile> apartmentSaveFiles { get; set; } = new List<ApartmentSaveFile>();
    }

   [Serializable]
    class ApartmentSaveFile
    {
        public int residenceNumber { get; set; }
        public int missedPayments { get; set; }
        public List<int> interactableIds { get; set; } = new List<int>();
    }
}
