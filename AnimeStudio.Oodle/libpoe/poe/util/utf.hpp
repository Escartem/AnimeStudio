#pragma once

#include <string>

namespace poe::util {
	struct less_without_case_predicate {
		bool operator()(std::u16string_view a, std::u16string_view b) const;
	};

	std::u16string lowercase(std::u16string_view s);

	std::string to_string(std::u16string_view s);
	std::u16string to_u16string(std::string_view s);
} // namespace poe::util