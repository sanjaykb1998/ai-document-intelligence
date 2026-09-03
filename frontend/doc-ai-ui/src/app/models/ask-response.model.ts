import { SearchResult } from './search-result.model';

export interface AskResponse {
  answer: string;
  sources: SearchResult[];
  Answer?: string;
  Sources?: SearchResult[];
}
