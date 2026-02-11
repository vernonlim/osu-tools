#!/usr/bin/env python3
"""
Convert CSV with preferred pp values to JSON format matching With_weighting.json structure.

This script:
1. Reads the CSV file with beatmap links, mod shorthand, and target pp values
2. Extracts beatmap IDs from the links
3. Matches them with existing score data from Preferred_pp_values.json to get Id, Statistics, etc.
4. Counts duplicates (same BeatmapID + Mods) for the Weighting field
5. Outputs in the With_weighting.json format
"""

import csv
import json
import re
from collections import Counter
from pathlib import Path


def parse_mod_shorthand(mod_string: str) -> list[dict]:
    """
    Convert mod shorthand (e.g., 'EZDT', 'HR', 'NM') to list of mod objects.
    
    NM (NoMod) returns empty list.
    Multi-mod strings like 'EZDT' are split into individual mods.
    """
    if not mod_string or mod_string.upper() == 'NM':
        return []
    
    # Known 2-character mod acronyms
    known_mods = {'EZ', 'NF', 'HT', 'HR', 'SD', 'PF', 'DT', 'NC', 'HD', 'FL', 'RL', 'AP', 'SO', 'AT', 'CN', 'MR'}
    
    mod_string = mod_string.upper()
    mods = []
    i = 0
    
    while i < len(mod_string):
        # Try to match 2-character mod
        if i + 2 <= len(mod_string):
            potential_mod = mod_string[i:i+2]
            if potential_mod in known_mods:
                mods.append({
                    "Acronym": potential_mod,
                    "Settings": {}
                })
                i += 2
                continue
        
        # If no match, skip character (shouldn't happen with valid input)
        i += 1
    
    return mods


def extract_beatmap_id(link: str) -> int | None:
    """Extract beatmap ID from osu.ppy.sh URL."""
    # Match patterns like:
    # https://osu.ppy.sh/beatmapsets/2039523#fruits/4254301
    # https://osu.ppy.sh/beatmapsets/2039523#osu/4254301
    match = re.search(r'#(?:fruits|osu|taiko|mania)/(\d+)', link)
    if match:
        return int(match.group(1))
    return None


def mods_to_key(mods: list[dict]) -> tuple:
    """Convert mods list to a hashable key for comparison."""
    return tuple(sorted(m.get('Acronym', '') for m in mods))


def main():
    script_dir = Path(__file__).parent
    
    csv_file = script_dir / "Preferred pp values - final list - Cut.csv"
    original_json = script_dir / "Preferred_pp_values.json"
    output_json = script_dir / "Converted_from_csv.json"
    
    # Load original JSON for score data lookup
    print(f"Loading original JSON: {original_json}")
    with open(original_json, 'r', encoding='utf-8') as f:
        original_data = json.load(f)
    
    # Build lookup: (BeatmapID, mods_tuple) -> score data
    score_lookup = {}
    for score in original_data.get('StoredScores', []):
        beatmap_id = score.get('BeatmapID')
        mods_key = mods_to_key(score.get('Mods', []))
        key = (beatmap_id, mods_key)
        if key not in score_lookup:
            score_lookup[key] = score
    
    print(f"Loaded {len(score_lookup)} unique beatmap+mod combinations from original JSON")
    
    # Read CSV and parse entries
    print(f"Reading CSV: {csv_file}")
    csv_entries = []
    
    with open(csv_file, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            link = row.get('Link', '')
            mod_shorthand = row.get('Mod', '')
            suggested_pp = row.get('Suggested pp value', '')
            
            beatmap_id = extract_beatmap_id(link)
            if beatmap_id is None:
                print(f"  Warning: Could not extract beatmap ID from: {link}")
                continue
            
            try:
                pp_value = float(suggested_pp) if suggested_pp else 0
            except ValueError:
                print(f"  Warning: Invalid pp value '{suggested_pp}' for beatmap {beatmap_id}")
                continue
            
            mods = parse_mod_shorthand(mod_shorthand)
            mods_key = mods_to_key(mods)
            
            csv_entries.append({
                'beatmap_id': beatmap_id,
                'mods': mods,
                'mods_key': mods_key,
                'pp_value': pp_value,
                'link': link
            })
    
    print(f"Parsed {len(csv_entries)} entries from CSV")
    
    # Count duplicates for weighting
    duplicate_counter = Counter((e['beatmap_id'], e['mods_key']) for e in csv_entries)
    
    # Track which entries we've already added (to avoid duplicates in output)
    added_entries = set()
    
    # Build output data
    stored_scores = []
    expected_performance = {}
    not_found = []
    
    for entry in csv_entries:
        key = (entry['beatmap_id'], entry['mods_key'])
        
        # Skip if we've already added this beatmap+mod combination
        if key in added_entries:
            continue
        added_entries.add(key)
        
        # Look up original score data
        original_score = score_lookup.get(key)
        
        if original_score:
            # Use existing score data, add weighting
            new_score = original_score.copy()
            new_score['Weighting'] = duplicate_counter[key]
            stored_scores.append(new_score)
            
            # Add expected performance
            score_id = new_score['Id']
            expected_performance[score_id] = {
                "Total": entry['pp_value'],
                "Skills": {}
            }
        else:
            not_found.append(entry)
    
    if not_found:
        print(f"\nWarning: {len(not_found)} entries from CSV not found in original JSON:")
        for entry in not_found[:10]:  # Show first 10
            print(f"  BeatmapID: {entry['beatmap_id']}, Mods: {entry['mods_key']}, Link: {entry['link']}")
        if len(not_found) > 10:
            print(f"  ... and {len(not_found) - 10} more")
    
    # Build output structure
    output_data = {
        "FileName": "Converted_from_csv.json",
        "Name": "Converted from CSV",
        "Scores": [],
        "StoredScores": stored_scores,
        "ExpectedPerformance": expected_performance
    }
    
    # Write output
    print(f"\nWriting output: {output_json}")
    with open(output_json, 'w', encoding='utf-8') as f:
        json.dump(output_data, f, indent=2)
    
    print(f"Done! Created {len(stored_scores)} entries with {len(expected_performance)} pp values")
    
    # Summary
    weighted_entries = [s for s in stored_scores if s.get('Weighting', 1) > 1]
    if weighted_entries:
        print(f"\nEntries with Weighting > 1: {len(weighted_entries)}")
        for score in weighted_entries[:5]:
            print(f"  BeatmapID: {score['BeatmapID']}, Mods: {mods_to_key(score.get('Mods', []))}, Weight: {score['Weighting']}")


if __name__ == '__main__':
    main()
