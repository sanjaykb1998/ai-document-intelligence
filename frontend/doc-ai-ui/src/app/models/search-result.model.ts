export interface SearchResult {
  documentId: string;
  fileName: string;
  chunkIndex: number;
  text: string;
  score: number;
}
