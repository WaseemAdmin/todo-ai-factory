import { TestBed } from '@angular/core/testing';
import { TodoApiService } from '../../core/api/todo-api.service';
import { Todo, TodoUpdate, TodoWrite } from '../../core/api/todo.model';
import { TodoPage } from './todo-page';

const initial: Todo = {
  id: 'one', title: 'First task', description: null, priority: 'Normal', dueAtUtc: null,
  isCompleted: false, completedAtUtc: null, createdAtUtc: '2026-11-18T10:00:00Z', updatedAtUtc: '2026-11-18T10:00:00Z'
};

class FakeApi {
  rows: Todo[] = [{ ...initial }];
  fail = false;
  async list(isCompleted?: boolean): Promise<Todo[]> {
    if (this.fail) throw new Error('offline');
    return this.rows.filter(row => isCompleted === undefined || row.isCompleted === isCompleted);
  }
  async get(id: string): Promise<Todo> { return { ...this.rows.find(row => row.id === id)! }; }
  async create(request: TodoWrite): Promise<Todo> {
    const row = { ...initial, ...request, id: 'new' };
    this.rows.push(row);
    return row;
  }
  async update(id: string, request: TodoUpdate): Promise<Todo> {
    const row = this.rows.find(row => row.id === id)!;
    Object.assign(row, request);
    row.completedAtUtc = request.isCompleted ? '2026-11-18T11:00:00Z' : null;
    return row;
  }
  async delete(id: string): Promise<void> { this.rows = this.rows.filter(row => row.id !== id); }
}

describe('TodoPage', () => {
  let api: FakeApi;
  let page: TodoPage;
  let fixture: ReturnType<typeof TestBed.createComponent<TodoPage>>;

  beforeEach(async () => {
    api = new FakeApi();
    await TestBed.configureTestingModule({ imports: [TodoPage], providers: [{ provide: TodoApiService, useValue: api }] }).compileComponents();
    fixture = TestBed.createComponent(TodoPage);
    page = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('renders the loaded list', () => {
    expect(fixture.nativeElement.textContent).toContain('First task');
    expect(page.todos()).toHaveLength(1);
  });

  it('loads details for a selected todo', async () => {
    await page.view(page.todos()[0]);
    expect(page.detail()?.id).toBe('one');
    await page.view(page.todos()[0]);
    expect(page.detail()).toBeNull();
  });

  it('shows a loading state while a list request is pending', async () => {
    let finish!: (rows: Todo[]) => void;
    vi.spyOn(api, 'list').mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
    const pending = page.reload();
    fixture.detectChanges();
    expect(page.loading()).toBe(true);
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent).toContain('Loading todos');
    finish([{ ...initial }]);
    await pending;
    expect(page.loading()).toBe(false);
  });

  it('creates and edits a todo', async () => {
    page.add();
    expect(page.editorOpen()).toBe(true);
    await page.save({ title: 'New task', description: null, priority: 'High', dueAtUtc: null });
    expect(page.editorOpen()).toBe(false);
    expect(page.todos().map(row => row.title)).toContain('New task');
    page.edit(page.todos().find(row => row.id === 'new')!);
    await page.save({ title: 'Renamed task', description: null, priority: 'Low', dueAtUtc: null });
    expect(page.todos().map(row => row.title)).toContain('Renamed task');
  });

  it('completes, filters, and reopens a todo', async () => {
    await page.toggle(page.todos()[0]);
    expect(page.todos()[0].isCompleted).toBe(true);
    page.setFilter('completed');
    await page.reload();
    expect(page.todos()).toHaveLength(1);
    await page.toggle(page.todos()[0]);
    expect(page.todos()).toHaveLength(0);
    page.setFilter('active');
    await page.reload();
    expect(page.todos()[0].isCompleted).toBe(false);
  });

  it('deletes only after confirmation', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    await page.remove(page.todos()[0]);
    expect(page.todos()).toHaveLength(1);
    confirm.mockReturnValue(true);
    await page.remove(page.todos()[0]);
    expect(page.todos()).toHaveLength(0);
    confirm.mockRestore();
  });

  it('shows API errors and clears the loading state', async () => {
    api.fail = true;
    await page.reload();
    fixture.detectChanges();
    expect(page.loading()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });
});
