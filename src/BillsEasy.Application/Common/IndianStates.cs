using System.Collections.Generic;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common;

public static class IndianStates
{
    public static readonly IReadOnlyList<IndianStateDto> All = new List<IndianStateDto>
    {
        new("Jammu and Kashmir", "01"),
        new("Himachal Pradesh", "02"),
        new("Punjab", "03"),
        new("Chandigarh", "04"),
        new("Uttarakhand", "05"),
        new("Haryana", "06"),
        new("Delhi", "07"),
        new("Rajasthan", "08"),
        new("Uttar Pradesh", "09"),
        new("Bihar", "10"),
        new("Sikkim", "11"),
        new("Arunachal Pradesh", "12"),
        new("Nagaland", "13"),
        new("Manipur", "14"),
        new("Mizoram", "15"),
        new("Tripura", "16"),
        new("Meghalaya", "17"),
        new("Assam", "18"),
        new("West Bengal", "19"),
        new("Jharkhand", "20"),
        new("Odisha", "21"),
        new("Chhattisgarh", "22"),
        new("Madhya Pradesh", "23"),
        new("Gujarat", "24"),
        new("Dadra and Nagar Haveli and Daman and Diu", "26"),
        new("Maharashtra", "27"),
        new("Andhra Pradesh", "28"),
        new("Karnataka", "29"),
        new("Goa", "30"),
        new("Lakshadweep", "31"),
        new("Kerala", "32"),
        new("Tamil Nadu", "33"),
        new("Puducherry", "34"),
        new("Andaman and Nicobar Islands", "35"),
        new("Telangana", "36"),
        new("Andhra Pradesh (New)", "37"),
        new("Ladakh", "38"),
        new("Other Territory", "97"),
        new("Centre Jurisdiction", "99")
    }.AsReadOnly();
}
