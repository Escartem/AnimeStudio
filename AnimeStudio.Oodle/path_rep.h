#pragma once

#include <string>
#include <vector>

std::vector<std::string> generate_paths(void const* spec_data, size_t spec_size);
void explain_paths(void const* spec_data, size_t spec_size);