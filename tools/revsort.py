import argparse
import sys
import os

def rev_sort(word: str, sub_len):
    return word[len(word) - sub_len:]
    

def main(dic_path: str, aff_path: str, sub_len: int):
    
    # Check file existence 
    if not os.path.exists(dic_path):
        print("\n[ERROR] .dic file not found. Exiting.")
        return
    
    if not os.path.exists(aff_path):
        print("\n[ERROR] .aff file not found. Exiting.")
        return
        
    # --- Your file processing logic would go here ---
    # Example: print the first line of each file
    
    dict = {}
    all_tags = set()

    with open(dic_path, 'r') as f_dic, open(aff_path, 'r') as f_aff:
        for line in f_dic:
            line = line.strip()

            # skip empty lines
            if not line:
                continue

            splits  = line.split('/')
            
            word = splits[0]
            tags = splits[1:]

            for tag in tags:
                all_tags.add(tag)
                if tag not in dict:
                    dict[tag] = [word]
                else:
                    dict[tag].append(word)
        
        for tag in all_tags:
            sorted(dict[tag], key=lambda word: word[(len(word) - sub_len):])
            uniq



if __name__ == "__main__":
    # 1. Create the parser
    parser = argparse.ArgumentParser(
        description="A script that processes two files with an optional labeled ID.",
        formatter_class=argparse.RawTextHelpFormatter
    )

    # 2. Add positional arguments (required filenames)
    parser.add_argument(
        'dic_path', 
        type=str, 
        help='The path to .dic file.'
    )
    
    parser.add_argument(
        'aff_path', 
        type=str, 
        help='The path to .aff file.'
    )

    # 3. Add an optional argument with a default value
    parser.add_argument(
        '-l', 
        '--length', 
        type=int, 
        default=1, 
        help='The length of the substring at the end of the word to sort. (default: 1)'
    )

    # 4. Parse the arguments
    args = parser.parse_args()

    # 5. Call the main function with the parsed arguments
    main(args.dic_path, args.aff_path, args.label)