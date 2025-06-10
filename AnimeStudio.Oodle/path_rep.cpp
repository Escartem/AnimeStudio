#include "path_rep.h"

#include "util.h"

/* The blocks in the tail section of the index bundle are generated through an
* alternating-phase algorithm where in the first phase a set of strings are built
* to serve as bases for the second phase where they are combined with suffixes
* to form the full outputs of the algorithm.
*
* u32 words control the generation and strings are null-terminated UTF-8.
* A zero word toggles the phase, input starts with a 0 to indicate the base phase.
*
* In the base phase a non-zero word inserts a new string into the base set or
* inserts a the concatenation of the base string and the following input.
*
* In the generation phase a non-zero word references a string in the base set
* and outputs the concatenation of the base string and the following input.
*
* The index words are one-based where 1 refers to the first string.
*
* A zero can toggle back to the generation phase in which all base strings shall
* be removed as if starting from scratch.
*
* The template section can be empty (two zero words after each other), typically
* done to emit a single string as-is.
*/
std::vector<std::string> generate_paths(void const* spec_data, size_t spec_size) {
	reader r(spec_data, spec_size);

	bool base_phase = false;
	std::vector<std::string> bases;
	std::vector<std::string> results;
	while (r.n_) {
		uint32_t cmd;
		if (!r.read(cmd)) {
			abort();
		}
		if (cmd == 0) {
			base_phase = !base_phase;
			if (base_phase) {
				bases.clear();
			}
		}
		if (cmd != 0) {
			std::string fragment;
			if (!r.read(fragment)) {
				abort();
			}

			// the input is one-indexed
			size_t index = cmd - 1;
			if (index < bases.size()) {
				// Back-ref to existing string, concatenate and insert/emit.
				std::string full = bases[index] + fragment;
				if (base_phase) {
					bases.push_back(full);
				}
				else {
					results.push_back(full);
				}
			}
			else {
				// New string, insert or emit as-is.
				if (base_phase) {
					bases.push_back(fragment);
				}
				else {
					results.push_back(fragment);
				}
			}
		}
	}

	return results;
}

void explain_paths(void const* spec_data, size_t spec_size) {
	reader r(spec_data, spec_size);

	bool base_phase = false;
	std::vector<std::string> bases;
	std::vector<std::string> results;
	while (r.n_) {
		uint32_t cmd;
		if (!r.read(cmd)) {
			abort();
		}
		fprintf(stderr, "Command %08u\n", cmd);
		if (cmd == 0) {
			base_phase = !base_phase;
			if (base_phase) {
				fprintf(stderr, "Entering template phase\n");
				bases.clear();
			}
			else {
				fprintf(stderr, "Entering generation phase\n");
			}
		}
		if (cmd != 0) {
			std::string fragment;
			if (!r.read(fragment)) {
				abort();
			}

			// the input is one-indexed
			size_t index = cmd - 1;
			if (index < bases.size()) {
				// Back-ref to existing string, concatenate and insert/emit.
				std::string full = bases[index] + fragment;
				if (base_phase) {
					bases.push_back(full);
					fprintf(stderr, "Add new reference %zu \"%s\" + \"%s\"\n", bases.size(), bases[index].c_str(), fragment.c_str());
				}
				else {
					results.push_back(full);
					fprintf(stderr, "Generating string \"%s\" + \"%s\"\n", bases[index].c_str(), fragment.c_str());
				}
			}
			else {
				// New string, insert or emit as-is.
				if (base_phase) {
					bases.push_back(fragment);
					fprintf(stderr, "Add new reference %zu \"%s\"\n", bases.size(), fragment.c_str());
				}
				else {
					results.push_back(fragment);
					fprintf(stderr, "Generate string \"%s\"\n", fragment.c_str());
				}
			}
		}
	}
}